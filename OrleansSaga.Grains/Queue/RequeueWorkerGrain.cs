using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains.Queue
{
    public class RequeueWorkerGrain : Grain, IRequeueWorkerGrain
    {
        protected Logger Log { get; set; }
        protected IDisposable Timer { get; set; }

        public override async Task OnActivateAsync()
        {
            Log = GetLogger($"RequeueWorkerGrain-{this.GetPrimaryKey(out string s)}");
            await base.OnActivateAsync();
        }

        public override Task OnDeactivateAsync()
        {
            return base.OnDeactivateAsync();
        }

        public Task Start(IRequeueGrain requeueGrain)
        {
            if (Timer != null)
            {
                return Task.CompletedTask;
            }
            Timer = RegisterTimer(StartCallback, requeueGrain, TimeSpan.Zero, TimeSpan.FromMilliseconds(-1));
            return Task.CompletedTask;
        }

        public Task Stop(IRequeueGrain requeueGrain)
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        protected async Task StartCallback(object state)
        {
            try
            {
                var requeueGrain = state as IRequeueGrain;
                for (var command = await requeueGrain.Dequeue(this); command != null; command = await requeueGrain.Dequeue(this))
                {
                    await Execute(requeueGrain, command);
                }
            }
            finally
            {
                Timer.Dispose();
                Timer = null;
            }
        }

        public async Task Execute(IRequeueGrain requeueGrain, GrainCommand command)
        {
            try
            {
                Log.Info($"Worker execute Command {command.CommandId}");
                await Task.Delay(10);
                //await requeueGrain.Schedule(new GrainCommand { CommandId = command.CommandId + 1000 }, TimeSpan.FromSeconds(10));
                await requeueGrain.Complete(command, this);
            }
            catch (Exception ex)
            {
                Log.Info($"Worker execute Command {command.CommandId} Exception: {ex}");
                await requeueGrain.Schedule(command, TimeSpan.FromSeconds(10));
            }
        }
    }

    public interface IRequeueWorkerGrain : IGrainWithIntegerCompoundKey
    {
        Task Start(IRequeueGrain requeueGrain);
        Task Stop(IRequeueGrain requeueGrain);
    }
}
