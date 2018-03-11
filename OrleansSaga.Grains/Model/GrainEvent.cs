using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OrleansSaga.Grains.Model
{
    public class GrainEvent<T> : GrainEvent where T : class, new()
    {
        private T _data;

        public new T Data => _data ?? (_data = GetData<T>());

        public GrainEvent(T data)
        {
            _data = data;
            EventType = typeof(T).FullName;
            base.Data = JsonConvert.SerializeObject(data);
            TaskStatus = TaskStatus.RanToCompletion;
            Created = DateTime.UtcNow;
        }
    }

    public class GrainEvent
    {
        public static GrainEvent FromMessage<T>(long grainId, T data) => FromMessage(grainId, data, typeof(T));

        public static GrainEvent FromMessage(long grainId, object data, Type dataType) => new GrainEvent
        {
            GrainId = grainId,
            EventType = dataType.FullName,
            Data = JsonConvert.SerializeObject(data),
            TaskStatus = TaskStatus.RanToCompletion,
            Created = DateTime.UtcNow,
        };

        public static GrainEvent FromException<T>(long grainId, Exception ex) => FromException(grainId, ex, typeof(T));

        public static GrainEvent FromException(long grainId, Exception ex, Type dataType) => new GrainEvent
        {
            GrainId = grainId,
            EventType = dataType.FullName,
            Data = JsonConvert.SerializeObject(ex),
            TaskStatus = TaskStatus.Faulted,
            Created = DateTime.UtcNow,
        };

        public static GrainEvent FromCancel<T>(long grainId) => FromCancel(grainId, typeof(T));

        public static GrainEvent FromCancel(long grainId, Type dataType) => new GrainEvent
        {
            GrainId = grainId,
            EventType = dataType.FullName,
            Data = JsonConvert.SerializeObject(new object()),
            TaskStatus = TaskStatus.Canceled,
            Created = DateTime.UtcNow,
        };

        public long GrainId { get; set; }
        public string EventType { get; set; }
        public string Data { get; set; }
        public TaskStatus TaskStatus { get; set; }
        public DateTime Created { get; set; }
        public object GetData() => JsonConvert.DeserializeObject(Data, Type.GetType(EventType));
        public T GetData<T>() => JsonConvert.DeserializeObject<T>(Data);

        public override string ToString() => $"GrainId: {GrainId}, EventType: {EventType}, TaskStatus: {TaskStatus}, Data: {Data}";
    }
}
