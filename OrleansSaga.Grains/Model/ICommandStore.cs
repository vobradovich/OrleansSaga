using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public interface ICommandStore
    {
        Task<IEnumerable<GrainCommandQueue>> GetQueuedCommands(Guid queueId);
        Task<GrainCommand> Get(long commandId);
        Task Add(params GrainCommand[] commands);
        Task<GrainCommandQueue> Enqueue(GrainCommand command, DateTime startDate, int tryCount);
        Task Complete(GrainCommandQueue commandQueue);
        Task Fail(GrainCommandQueue commandQueue, Exception ex);
    }
}
