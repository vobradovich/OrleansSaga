using System;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public interface ICommandStore
    {
        Task<GrainCommandQueue[]> GetQueuedCommands(Guid queueId);
        Task<GrainCommand> Get(long commandId);
        Task Add(params GrainCommand[] commands);
        Task<GrainCommandQueue> Enqueue(GrainCommand command);
        Task<GrainCommandQueue> Enqueue(GrainCommand command, DateTime startDate);
        Task Complete(GrainCommand command);
        Task Fail(GrainCommand command, Exception ex);
    }
}
