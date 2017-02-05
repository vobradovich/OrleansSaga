using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        protected Queue<GrainCommandQueue> CurrentQueue { get; set; }
        protected Dictionary<Type, Func<object, Task>> Dispatchers { get; private set; }

        public CommandQueueGrain(ICommandStore commandStore, IBackoffProvider backoffProvider)
        {
            CommandStore = commandStore;
            BackoffProvider = backoffProvider ?? new FixedBackoff(TimeSpan.FromMinutes(1));
        }

        public override async Task OnActivateAsync()
        {
            var grainId = this.GetPrimaryKey();
            Log = GetLogger($"QueueGrain-{grainId}");

            var commands = await CommandStore.GetQueuedCommands(grainId);
            foreach(var c in commands)
            {
                Enqueue(c);
            }
            RegisterTimer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
            await base.OnActivateAsync();
        }

        protected async Task TimerCallback(object state)
        {
            var queuedCommand = CurrentQueue.Dequeue();
            while (queuedCommand != null)
            {
                await Dispatch(queuedCommand);
                queuedCommand = CurrentQueue.Dequeue();
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
                    await dispatcher(commandQueue.Command);
                }
                await CommandStore.Complete(commandQueue.Command);
            }
            catch (Exception ex)
            {
                var delay = BackoffProvider.Next(commandQueue.TryCount);
                await CommandStore.Fail(commandQueue.Command, ex);
                var q = await CommandStore.Enqueue(commandQueue.Command, DateTime.UtcNow + delay, commandQueue.TryCount + 1);
                Enqueue(q);
            }
        }

        public async Task Add(GrainCommand command)
        {
            await CommandStore.Add(command);
            var q = await CommandStore.Enqueue(command);
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
