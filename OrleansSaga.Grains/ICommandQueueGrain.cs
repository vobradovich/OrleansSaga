using System;
using System.Threading.Tasks;
using Orleans;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public interface ICommandQueueGrain : IGrainWithGuidKey
    {
        Task Register(Type type, Func<object, Task> dispatcher);
        Task Start(TimeSpan interval);
        Task Add(object command);
    }
}
