using System.Threading.Tasks;
using Orleans;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public interface ICommandQueueGrain : IGrainWithGuidKey
    {
        Task Add(GrainCommand command);
    }
}
