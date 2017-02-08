using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public class CommandQueueGrain : Grain, ICommandQueueGrain
    {
        protected Logger Log { get; set; }
        protected ICommandStore CommandStore { get; set; }
        protected IBackoffProvider BackoffProvider { get; set; }
        protected Queue<GrainCommandQueue> CurrentQueue { get; private set; } = new Queue<GrainCommandQueue>();
        protected Dictionary<Type, Func<object, Task>> Dispatchers { get; private set; } = new Dictionary<Type, Func<object, Task>>();
        protected IDisposable Timer { get; set; }

        public CommandQueueGrain(ICommandStore commandStore, IBackoffProvider backoffProvider)
        {
            CommandStore = commandStore;
            BackoffProvider = backoffProvider ?? new FixedBackoff(TimeSpan.FromSeconds(1));
        }

        public override async Task OnActivateAsync()
        {
            var grainId = this.GetPrimaryKey();
            Log = GetLogger($"QueueGrain-{grainId}");

            Register<StartMessage>(async m =>
            {
                var queueGrain = GrainFactory.GetGrain<ICommandQueueGrain>(Guid.Empty);
                await queueGrain.Start(TimeSpan.FromSeconds(1));
                await queueGrain.Add(new DoneMessage());
            });
            Register<DoneMessage>(m =>
            {
                return TaskDone.Done;
            });

            await base.OnActivateAsync();
        }

        public void Register<T>(Func<T, Task> dispatcher) where T : class
        {
            if (!Dispatchers.ContainsKey(typeof(T)))
            {
                Dispatchers.Add(typeof(T), (o) => dispatcher((T)o));
            }            
        }

        public Task Register(Type type, Func<object, Task> dispatcher)
        {
            if (!Dispatchers.ContainsKey(type))
            {
                Dispatchers.Add(type, dispatcher);
            }
            return TaskDone.Done;
        }

        public async Task Start(TimeSpan interval)
        {
            if (Timer == null)
            {
                var grainId = this.GetPrimaryKey();
                var commands = await CommandStore.GetQueuedCommands(grainId);
                foreach (var c in commands)
                {
                    Enqueue(c);
                }
                Timer = RegisterTimer(TimerCallback, null, TimeSpan.Zero, interval);
            }
        }

        public async Task Stop()
        {
            if (Timer != null)
            {
                Timer.Dispose();
                Timer = null;
            }
        }

        protected async Task TimerCallback(object state)
        {   
            while (CurrentQueue.Count > 0)
            {
                var queuedCommand = CurrentQueue.Dequeue();
                await Dispatch(queuedCommand);
            }
        }

        protected virtual async Task Dispatch(GrainCommandQueue commandQueue)
        {
            Log.Info($"Dispatch {commandQueue}");
            try
            {
                var commandType = Type.GetType(commandQueue.Command.CommandType);
                Func<object, Task> dispatcher;
                if (Dispatchers.TryGetValue(commandType, out dispatcher))
                {
                    await dispatcher(commandQueue.Command.GetData());
                    await CommandStore.Complete(commandQueue);
                }
                else
                {
                    await Reenqueue(commandQueue, new NotImplementedException());
                }
            }
            catch (Exception ex)
            {
                await Reenqueue(commandQueue, ex);
            }
        }

        private async Task Reenqueue(GrainCommandQueue commandQueue, Exception ex)
        {            
            var delay = BackoffProvider.Next(commandQueue.TryCount);
            await CommandStore.Fail(commandQueue, ex);
            var q = await CommandStore.Enqueue(commandQueue.Command, DateTime.UtcNow + delay, commandQueue.TryCount + 1);
            Enqueue(q);
        }

        public async Task Add(object o)
        {
            var grainId = this.GetPrimaryKey();
            var command = new GrainCommand
            {
                QueueId = grainId,
                CommandType = o.GetType().FullName,
                CommandData = JsonConvert.SerializeObject(o)
            };
            await Add(command);
        }

        public async Task Add<T>(T o) where T : class
        {
            var grainId = this.GetPrimaryKey();
            var command = new GrainCommand<T>(o) { QueueId = grainId };
            await Add(command);
        }

        public async Task Add(GrainCommand command)
        {
            await CommandStore.Add(command);
            var q = await CommandStore.Enqueue(command, DateTime.UtcNow, 0);
            Enqueue(q);
        }


        public void Enqueue(GrainCommandQueue commandQueue)
        {
            var delay = commandQueue.StartDate - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                CurrentQueue.Enqueue(commandQueue);
            }
            else
            {
                RegisterTimer((cq) =>
                {
                    CurrentQueue.Enqueue(cq as GrainCommandQueue);
                    return TaskDone.Done;
                }, commandQueue, delay, TimeSpan.FromMilliseconds(-1));
            }
        }
    }
}
