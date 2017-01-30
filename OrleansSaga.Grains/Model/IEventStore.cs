using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public interface IEventStore
    {
        Task<IEnumerable<GrainEvent>> LoadEvents(long grainId);

        Task AddEvents(params GrainEvent[] events);
    }
}
