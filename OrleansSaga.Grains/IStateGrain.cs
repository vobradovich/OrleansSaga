using System;
using System.Threading.Tasks;
using Orleans;

namespace OrleansSaga.Grains
{
    public interface IStateGrain : IGrainWithIntegerKey
    {
        Task Create();
        Task Cancel(string reason);
    }
}