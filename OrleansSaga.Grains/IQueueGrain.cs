using System.Threading.Tasks;
using Orleans;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public interface IQueueGrain : IGrainWithGuidKey
    {
        Task Enqueue(GrainCommand command);
    }
}
