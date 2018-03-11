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
        protected IDisposable Timer { get; set; }
        protected Stack<IRequeueWorkerGrain> WorkerPool { get; private set; } = new Stack<IRequeueWorkerGrain>();
        protected IRequeueStore Store { get; set; }
        protected IBackoffProvider BackoffProvider { get; set; }
        protected int MaxTryCount { get; set; } = 5;

        public RequeueGrain()
        {

        }

        public override async Task OnActivateAsync()
        {
            var grainId = this.GetPrimaryKeyString();
            Log = GetLogger($"RequeueGrain-{grainId}");
            var interval = TimeSpan.FromSeconds(5);
            Timer = RegisterTimer(SchedulerCallback, null, TimeSpan.Zero, interval);
            BackoffProvider = new FibonacciBackoff(TimeSpan.FromSeconds(5));
            for (long i = 0; i < 1; i++)
            {
                var worker = GrainFactory.GetGrain<IRequeueWorkerGrain>(i, grainId, null);
                WorkerPool.Push(worker);
            }
            Store = new SqlRequeueStore(grainId);
            await Store.Load();            
            Store.Assigned.ForEach(async a => await Fail(a, "Fail Assigned"));            
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

        public async Task Enqueue(params long[] commandIds)
        {
            await Store.Enqueue(commandIds);
            while (Store.Queued.Count > 0 && WorkerPool.Count > 0)
            {
                var worker = WorkerPool.Pop();
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} WorkerPool Pop {worker.GetPrimaryKey(out string s)}");
                await worker.Start(this);
            }
        }

        public async Task<long?> Dequeue(IRequeueWorkerGrain worker)
        {
            var command = await Store.Assign(worker.GetPrimaryKeyLong(out string s));
            if (command == null)
            {
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} WorkerPool Push {worker.GetPrimaryKey(out string s1)}");
                WorkerPool.Push(worker);
                return null as long?;
            }
            //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Assign Command {command.CommandId} Worker {worker.GetPrimaryKey(out string s)}");
            return command.CommandId;
        }

        public async Task Complete(long commandId, IRequeueWorkerGrain worker)
        {
            //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Complete Command {command.CommandId} Worker {worker.GetPrimaryKey(out string s)}");
            await Store.Complete(commandId, worker.GetPrimaryKeyLong(out string s));
        }

        public async Task Fail(long commandId, IRequeueWorkerGrain worker, string reason)
        {
            var workerId = worker.GetPrimaryKeyLong(out string s);
            Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Fail Command {commandId} Worker {workerId}");
            var assigned = Store.Assigned.Find(c => c.CommandId == commandId);
            if ((assigned?.TryCount + 1 ?? 0) > MaxTryCount)
            {
                await Store.Fail(commandId, workerId, reason);
                return;
            }

            var delay = BackoffProvider.Next(assigned.TryCount + 1);
            await Schedule(delay, commandId);
        }

        public async Task Fail(RequeueCommandAssigned assigned, string reason)
        {
            if (assigned.TryCount + 1 > MaxTryCount)
            {
                await Store.Fail(assigned.CommandId, assigned.WorkerId, reason);
                return;
            }

            var delay = BackoffProvider.Next(assigned.TryCount + 1);
            await Schedule(delay, assigned.CommandId);
        }

        public Task Schedule(TimeSpan timeSpan, params long[] commandIds)
        {
            return Schedule(DateTimeOffset.Now.Add(timeSpan), commandIds);
        }

        public Task Schedule(DateTimeOffset dateTime, params long[] commandIds)
        {
            if (dateTime <= DateTimeOffset.Now)
            {
                return Enqueue(commandIds);
            }
            return Store.Schedule(dateTime, commandIds);
        }

        protected async Task SchedulerCallback(object state)
        {
            try
            {
                //Log.Info($"RequeueGrain {this.GetPrimaryKeyString()} Stats - Enqueued: {Store.Queued.Count}, Scheduled: {Store.Scheduled.Count}, Finished: {Store.Finished.Count}, Worker Pool: {WorkerPool.Count}, Assigned: {Store.Assigned.Count}");
                var enqueue = await Store.GetScheduled(DateTimeOffset.Now);
                if (enqueue.Count() > 0)
                {
                    await Enqueue(enqueue);
                }
                //if(Store.Queued.Count > 0)
                //{
                //    DelayDeactivation(TimeSpan.FromMinutes(5));
                //}
                //if (Store.Queued.Count == 0 && Store.Scheduled.Count == 0)
                //{
                //    DeactivateOnIdle();
                //}                
                //return Task.WhenAll(WorkerPool.Select(w => w.Stop(this)));                
            }
            catch (Exception ex)
            {
                Log.Error(0, $"RequeueGrain {this.GetPrimaryKeyString()} Exception", ex);
            }
        }
    }

    public interface IRequeueGrain : IGrainWithStringKey
    {
        Task<long?> Dequeue(IRequeueWorkerGrain worker);
        Task Complete(long commandId, IRequeueWorkerGrain worker);
        Task Fail(long commandId, IRequeueWorkerGrain worker, string reason);
        Task Enqueue(params long[] commandIds);
        Task Schedule(TimeSpan timeSpan, params long[] commandIds);
        Task Schedule(DateTimeOffset dateTime, params long[] commandIds);
    }
}
