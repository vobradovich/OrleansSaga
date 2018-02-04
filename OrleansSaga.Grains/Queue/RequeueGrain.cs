using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains.Queue
{
    public class RequeueGrain : Grain, IRequeueGrain
    {
        protected Logger Log { get; set; }
        protected Queue<GrainCommand> CurrentQueue { get; private set; } = new Queue<GrainCommand>();
        protected Dictionary<GrainCommand, DateTimeOffset> Scheduled { get; private set; } = new Dictionary<GrainCommand, DateTimeOffset>();
        protected IDisposable Timer { get; set; }
        protected Stack<IRequeueWorkerGrain> WorkerPool { get; private set; } = new Stack<IRequeueWorkerGrain>();
        protected Dictionary<IRequeueWorkerGrain, GrainCommand> WorkerAssigned { get; private set; } = new Dictionary<IRequeueWorkerGrain, GrainCommand>();
        protected List<GrainCommand> CompleteCommands { get; private set; } = new List<GrainCommand>();

        public RequeueGrain()
        {

        }

        public override async Task OnActivateAsync()
        {
            var grainId = this.GetPrimaryKeyString();
            Log = GetLogger($"RequeueGrain-{grainId}");
            var interval = TimeSpan.FromSeconds(5);
            Timer = RegisterTimer(SchedulerCallback, null, TimeSpan.Zero, interval);
            for (long i = 0; i < 1; i++)
            {
                var worker = GrainFactory.GetGrain<IRequeueWorkerGrain>(i, grainId, null);
                WorkerPool.Push(worker);
            }
            await base.OnActivateAsync();
        }

        public override Task OnDeactivateAsync()
        {
            Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Deactivate");
            if (Timer != null)
            {
                Timer.Dispose();
                Timer = null;
            }
            return base.OnDeactivateAsync();
        }

        public async Task Enqueue(params GrainCommand[] commands)
        {
            commands
                .ToList()
                .ForEach(c =>
                {
                    //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Enqueue Command {c.CommandId}");
                    CurrentQueue.Enqueue(c);
                });
            while (WorkerPool.Count > 0 && CurrentQueue.Count > 0)
            {
                var worker = WorkerPool.Pop();
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} WorkerPool Pop {worker.GetPrimaryKey(out string s)}");
                await worker.Start(this);
            }
        }

        public Task<GrainCommand> Dequeue(IRequeueWorkerGrain worker)
        {
            if (WorkerAssigned.ContainsKey(worker))
            {
                WorkerAssigned.Remove(worker);
            }
            if (CurrentQueue.Count == 0)
            {
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} WorkerPool Push {worker.GetPrimaryKey(out string s1)}");
                WorkerPool.Push(worker);
                return Task.FromResult(null as GrainCommand);
            }
            var command = CurrentQueue.Dequeue();
            WorkerAssigned.Add(worker, command);
            //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Assign Command {command.CommandId} Worker {worker.GetPrimaryKey(out string s)}");
            return Task.FromResult(command);
        }

        public async Task Complete(GrainCommand command, IRequeueWorkerGrain worker)
        {
            //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Complete Command {command.CommandId} Worker {worker.GetPrimaryKey(out string s)}");
            if (WorkerAssigned.ContainsKey(worker))
            {
                WorkerAssigned.Remove(worker);
            }
            CompleteCommands.Add(command);
        }

        public Task Schedule(GrainCommand command, TimeSpan timeSpan)
        {
            return Schedule(command, DateTimeOffset.Now.Add(timeSpan));
        }

        public Task Schedule(GrainCommand command, DateTimeOffset dateTime)
        {
            if (dateTime <= DateTimeOffset.Now)
            {
                return Enqueue(command);
            }
            if (!Scheduled.ContainsKey(command))
            {
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Schedule Command {command.CommandId}");
                Scheduled.Add(command, dateTime);
            }
            return Task.CompletedTask;
        }

        protected Task SchedulerCallback(object state)
        {
            try
            {
                Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Stats - Enqueued: {CurrentQueue.Count}, Scheduled: {Scheduled.Count}, Completed: {CompleteCommands.Count}, Worker Pool: {WorkerPool.Count}, Assigned: {WorkerAssigned.Count}");
                var enqueue = Scheduled.Where(k => k.Value <= DateTimeOffset.Now).Select(k => k.Key).ToList();
                enqueue.ForEach(c => Scheduled.Remove(c));
                if (CurrentQueue.Count > 0 || Scheduled.Count > 0)
                {
                    return Enqueue(enqueue.ToArray());
                }
                DeactivateOnIdle();
                //return Task.WhenAll(WorkerPool.Select(w => w.Stop(this)));
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(0, $"RequeueGrain {this.GetPrimaryKeyString()} Exception", ex);
                return Task.CompletedTask;
            }
        }
    }

    public interface IRequeueGrain : IGrainWithStringKey
    {
        Task<GrainCommand> Dequeue(IRequeueWorkerGrain worker);
        Task Complete(GrainCommand command, IRequeueWorkerGrain worker);
        Task Enqueue(params GrainCommand[] commands);
        Task Schedule(GrainCommand command, TimeSpan timeSpan);
        Task Schedule(GrainCommand command, DateTimeOffset dateTime);
    }
}
