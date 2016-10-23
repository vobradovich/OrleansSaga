using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class StateCommand<T> : StateCommand
    {
        public new T Data { get; set; }
    }

    public class StateCommand
    {
        public long Id { get; set; }
        public long StateGrainId { get; set; }
        public string Name { get; set; }
        public string Data { get; set; }
        public string Repeats { get; set; }
        public DateTime Created { get; set; }
        public DateTime ExecuteAt { get; set; }
    }
}
