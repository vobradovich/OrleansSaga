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
        ConcurrentDictionary<long, List<GrainEvent>> Events = new ConcurrentDictionary<long, List<GrainEvent>>();

        public Task<IEnumerable<GrainEvent>> LoadEvents(long grainId)
        {
            List<GrainEvent> events = null;
            if (Events.ContainsKey(grainId) && Events.TryGetValue(grainId, out events))
            {
                return Task.FromResult(events.AsEnumerable());
            }
            return Task.FromResult(Enumerable.Empty<GrainEvent>());
        }

        public Task AddEvents(params GrainEvent[] events)
        {
            foreach (var ev in events)
            {
                if (!Events.ContainsKey(ev.GrainId))
                {
                    Events.TryAdd(ev.GrainId, new List<GrainEvent>());
                }
                Events[ev.GrainId].Add(ev);
            }
            return Task.FromResult(0) as Task;
        }
    }
}
