using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Orleans;
using Orleans.Runtime;
using Remote.Linq;

namespace OrleansSaga.Grains.Queue
{
    public class RequeueWorkerGrain : Grain, IRequeueWorkerGrain
    {
        protected Logger Log { get; set; }
        protected IDisposable Timer { get; set; }

        public override async Task OnActivateAsync()
        {
            var key = this.GetPrimaryKeyLong(out string s);
            Log = GetLogger($"RequeueWorkerGrain-{s}-{key}");
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
            //Timer = RegisterTimer(StartCallback, requeueGrain, TimeSpan.Zero, TimeSpan.FromMilliseconds(-1));
            StartCallback(requeueGrain).Ignore();
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
                for (var commandId = await requeueGrain.Dequeue(this); commandId.HasValue; commandId = await requeueGrain.Dequeue(this))
                {
                    await Execute(requeueGrain, commandId.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(0, $"Exception {ex}");
                //Console.ReadKey();
            }
            finally
            {
                if (Timer != null)
                {
                    Timer.Dispose();
                    Timer = null;
                }
            }
        }

        public async Task Execute(IRequeueGrain requeueGrain, long commandId)
        {
            try
            {
                var command = GrainFactory.GetGrain<ICommandGrain>(commandId);
                await command.Execute();
                await requeueGrain.Complete(commandId, this);
            }
            catch (Exception ex)
            {
                Log.Warn(0, $"Worker execute Command {commandId} Exception: {ex}");
                await requeueGrain.Fail(commandId, this, ex.ToString());
            }
        }
    }

    public interface IRequeueWorkerGrain : IGrainWithIntegerCompoundKey
    {
        Task Start(IRequeueGrain requeueGrain);
        Task Stop(IRequeueGrain requeueGrain);
    }
}
