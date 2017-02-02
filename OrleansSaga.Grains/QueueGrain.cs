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
    public class QueueGrain : Grain, IQueueGrain
    {
        protected Logger Log { get; set; }
        protected ICommandStore CommandStore { get; set; }
        protected Queue<GrainCommandQueue> Queue { get; set; }

        public QueueGrain(ICommandStore commandStore)
        {
            CommandStore = commandStore;
        }

        public override async Task OnActivateAsync()
        {
            var grainId = this.GetPrimaryKey();
            Log = GetLogger($"QueueGrain-{grainId}");

            var commands = await CommandStore.GetQueuedCommands(grainId);
            foreach(var c in commands)
            {
                Queue.Enqueue(c);
            }

            RegisterTimer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            await base.OnActivateAsync();
        }

        protected async Task TimerCallback(object state)
        {
            foreach(var queuedCommand in Queue)
            {
                await Dispatch(queuedCommand.Command);
            }
        }

        protected virtual async Task Dispatch(GrainCommand command)
        {
            Log.Info($"Dispatch {command}");
            try
            {
                var commandType = Type.GetType(command.CommandType);

            }
            catch (Exception ex)
            {
                //var delay = current == 0 ? TimeSpan.Zero : backoffProvider.Next(current);
                var delay = TimeSpan.FromMinutes(1);
                await CommandStore.Fail(command, ex);
                var q = await CommandStore.Enqueue(command, DateTime.UtcNow + delay);
                RegisterTimer((cq) => {
                    Queue.Enqueue(cq as GrainCommandQueue);
                    return TaskDone.Done;
                    }, q, delay, TimeSpan.FromMilliseconds(-1));
            }
        }

        public async Task Enqueue(GrainCommand command)
        {
            await CommandStore.Add(command);
            var q = await CommandStore.Enqueue(command);
            Queue.Enqueue(q);
        }
    }
}
