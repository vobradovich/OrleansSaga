using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class RequeueStore : IRequeueStore
    {
        public string QueueId { get; }

        public RequeueStore(string queueId)
        {
            QueueId = queueId;
        }

        public Queue<RequeueCommandEnqueued> Queued { get; } = new Queue<RequeueCommandEnqueued>();
        public List<RequeueCommandScheduled> Scheduled { get; } = new List<RequeueCommandScheduled>();
        public List<RequeueCommandFinished> Finished { get; } = new List<RequeueCommandFinished>();
        public List<RequeueCommandAssigned> Assigned { get; } = new List<RequeueCommandAssigned>();

        public Task<RequeueCommandAssigned> Dequeue(long workerId)
        {
            Assigned.RemoveAll(a => a.WorkerId == workerId);
            if (Queued.Count > 0)
            {
                var command = Queued.Dequeue();
                var assigned = new RequeueCommandAssigned { QueueId = QueueId, CommandId = command.CommandId, WorkerId = workerId, TryCount = command.TryCount };
                Assigned.Add(assigned);
                return Task.FromResult(assigned);
            }
            return Task.FromResult(null as RequeueCommandAssigned);
        }

        public Task Enqueue(params long[] commandIds)
        {
            foreach (var commandId in commandIds)
            {
                var scheduled = Scheduled.Find(a => a.CommandId == commandId);
                Scheduled.RemoveAll(a => a.CommandId == commandId);
                Queued.Enqueue(new RequeueCommandEnqueued { QueueId = QueueId, CommandId = commandId, TryCount = scheduled?.TryCount ?? 0 });
            }
            return Task.CompletedTask;
        }

        public Task Schedule(DateTimeOffset dateTime, params long[] commandIds)
        {
            foreach (var commandId in commandIds)
            {
                var assigned = Assigned.Find(a => a.CommandId == commandId);
                Assigned.RemoveAll(a => a.CommandId == commandId);                
                Scheduled.Add(new RequeueCommandScheduled { QueueId = QueueId, CommandId = commandId, Scheduled = dateTime, TryCount = assigned?.TryCount + 1 ?? 0});
            }
            return Task.CompletedTask;
        }

        public Task Complete(long commandId, long workerId)
        {
            var assigned = Assigned.Find(a => a.CommandId == commandId);
            Assigned.RemoveAll(a => a.CommandId == commandId);
            Finished.Add(new RequeueCommandFinished { QueueId = QueueId, CommandId = commandId, Status = "Complete", TryCount = assigned?.TryCount ?? 0 });
            return Task.CompletedTask;
        }

        public Task Fail(long commandId, long workerId, string reason)
        {
            var assigned = Assigned.Find(a => a.CommandId == commandId);
            Assigned.RemoveAll(a => a.CommandId == commandId);
            Finished.Add(new RequeueCommandFinished { QueueId = QueueId, CommandId = commandId, Status = "Fail", Reason = reason, TryCount = assigned?.TryCount ?? 0 });
            return Task.CompletedTask;
        }

        public Task<long[]> GetScheduled(DateTimeOffset dateTime)
        {
            var scheduled = Scheduled
                .Where(k => k.Scheduled <= dateTime)
                .Select(c => c.CommandId).ToArray();
            return Task.FromResult(scheduled);
        }
    }

    public interface IRequeueStore
    {
        Queue<RequeueCommandEnqueued> Queued { get; }
        List<RequeueCommandScheduled> Scheduled { get; }
        List<RequeueCommandFinished> Finished { get; }
        List<RequeueCommandAssigned> Assigned { get; }
        Task Enqueue(params long[] commandIds);
        Task Schedule(DateTimeOffset dateTime, params long[] commandIds);
        Task<long[]> GetScheduled(DateTimeOffset dateTime);
        Task<RequeueCommandAssigned> Dequeue(long workerId);
        Task Complete(long commandId, long workerId);
        Task Fail(long commandId, long workerId, string reason);
    }
}
