using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public class MemoryEventStore : IEventStore
    {
        ConcurrentDictionary<long, List<StateEvent>> Events = new ConcurrentDictionary<long, List<StateEvent>>();

        public Task<IEnumerable<StateEvent>> LoadEvents(long grainId)
        {
            List<StateEvent> events = null;
            if (Events.ContainsKey(grainId) && Events.TryGetValue(grainId, out events))
            {
                return Task.FromResult(events.AsEnumerable());
            }
            return Task.FromResult(Enumerable.Empty<StateEvent>());
        }

        public Task AddEvents(params StateEvent[] events)
        {
            foreach (var ev in events)
            {
                if (!Events.ContainsKey(ev.GrainId))
                {
                    Events.TryAdd(ev.GrainId, new List<StateEvent>());
                }
                Events[ev.GrainId].Add(ev);
            }
            return Task.FromResult(0) as Task;
        }
    }
}
