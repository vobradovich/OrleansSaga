using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OrleansSaga.Grains.Model
{
    public class RequeueCommand<T> : RequeueCommand where T : class
    {
        private T _data;

        public new T CommandData
        {
            get { return _data ?? (_data = GetData<T>()); }
        }

        public RequeueCommand(T data)
        {
            _data = data;
            CommandType = typeof(T).FullName;
            base.CommandData = JsonConvert.SerializeObject(data);
            Created = DateTime.UtcNow;
        }
    }

    public class RequeueCommand
    {
        public long CommandId { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
        public string CommandType { get; set; }
        public string CommandData { get; set; }
        public object GetData() => JsonConvert.DeserializeObject(CommandData, Type.GetType(CommandType));
        public T GetData<T>() => JsonConvert.DeserializeObject<T>(CommandData);
        public override string ToString() => $"CommandId: {CommandId}, CommandType: {CommandType}, CommandData: {CommandData}";
    }

    public class RequeueCommandEnqueued
    {
        public long Id { get; set; }
        public string QueueId { get; set; }
        public long CommandId { get; set; }
        public int TryCount { get; set; } = 0;
        public DateTimeOffset Enqueued { get; set; } = DateTimeOffset.Now;
        public override string ToString() => $"CommandId: {CommandId}, QueueId: {QueueId}, Enqueued: {Enqueued}, TryCount: {TryCount}";
    }

    public class RequeueCommandAssigned
    {
        public long Id { get; set; }
        public string QueueId { get; set; }
        public long CommandId { get; set; }
        public long WorkerId { get; set; }
        public int TryCount { get; set; } = 0;
        public DateTimeOffset Assigned { get; set; } = DateTimeOffset.Now;
        public override string ToString() => $"CommandId: {CommandId}, QueueId: {QueueId}, Assigned: {Assigned}, TryCount: {TryCount}";
    }

    public class RequeueCommandScheduled
    {
        public long Id { get; set; }
        public string QueueId { get; set; }
        public long CommandId { get; set; }
        public int TryCount { get; set; } = 0;
        public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset Scheduled { get; set; }
        public override string ToString() => $"CommandId: {CommandId}, QueueId: {QueueId}, Scheduled: {Scheduled}, TryCount: {TryCount}";
    }

    public class RequeueCommandFinished
    {
        public long Id { get; set; }
        public string QueueId { get; set; }
        public long CommandId { get; set; }
        public int TryCount { get; set; } = 0;
        public DateTimeOffset Finished { get; set; } = DateTimeOffset.Now;
        public string Status { get; set; }
        public string Reason { get; set; }
        public override string ToString() => $"CommandId: {CommandId}, QueueId: {QueueId}, Finished: {Finished}, TryCount: {TryCount}";
    }

    public class RequeueCommandLog
    {
        public long Id { get; set; }
        public string QueueId { get; set; }
        public long CommandId { get; set; }
        public int TryCount { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
    }
}
