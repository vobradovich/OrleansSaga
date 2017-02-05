using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OrleansSaga.Grains.Model
{
    public class GrainCommand<T> : GrainCommand where T : class, new()
    {
        private T _data;

        public new T CommandData
        {
            get { return _data ?? (_data = GetData<T>()); }
        }

        public GrainCommand(T data)
        {
            _data = data;
            CommandType = typeof(T).FullName;
            base.CommandData = JsonConvert.SerializeObject(data);
            Created = DateTime.UtcNow;
        }
    }

    public class GrainCommand
    {
        public long CommandId { get; set; }
        public Guid QueueId { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public string CommandType { get; set; }
        public string CommandData { get; set; }
        public object GetData() => JsonConvert.DeserializeObject(CommandData, Type.GetType(CommandType));
        public T GetData<T>() => JsonConvert.DeserializeObject<T>(CommandData);
        public override string ToString() => $"CommandId: {CommandId}, QueueId: {QueueId}, CommandType: {CommandType}, CommandData: {CommandData}";
    }

    public class GrainCommandQueue
    {
        public long CommandId { get; set; }
        public int TryCount { get; set; } = 0;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public GrainCommand Command { get; set; }
        public override string ToString() => $"CommandId: {CommandId}, StartDate: {StartDate}, Command: {Command}";
    }

    public class GrainCommandLog
    {
        public long CommandId { get; set; }
        public int TryCount { get; set; }
        public DateTime CompleteDate { get; set; }
        public string CommandStatus { get; set; }
        public string CommandResult { get; set; }
    }
}
