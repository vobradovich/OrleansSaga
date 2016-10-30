using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class MemoryEventStore : IEventStore
    {
        List<StateEvent> Events = new List<StateEvent>();

        public Task<IEnumerable<StateEvent>> LoadEvents(long grainId)
        {
            return Task.FromResult(Events.Where(e => e.GrainId == grainId));
        }

        public Task AddEvents(params StateEvent[] events)
        {
            Events.AddRange(events);
            return Task.FromResult(0) as Task;
        }
    }
}
