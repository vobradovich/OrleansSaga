using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OrleansSaga.Grains.Model
{
    public class StateEvent<T> : StateEvent where T: class, new()
    {
        private T _data;

        public new T Data
        {
            get { return _data ?? (_data = GetData<T>()); }
        }

        public StateEvent(T data)
        {
            _data = data;
            EventType = typeof(T).FullName;
            base.Data = JsonConvert.SerializeObject(data);
            Created = DateTime.UtcNow;
        }        
    }

    public class StateEvent
    {
        public long Id { get; set; }
        public long GrainId { get; set; }
        public string EventType { get; set; }
        public string Data { get; set; }
        public DateTime Created { get; set; }
        public object GetData() => JsonConvert.DeserializeObject(Data, Type.GetType(EventType));
        public T GetData<T>() => JsonConvert.DeserializeObject<T>(Data);
    }
}
