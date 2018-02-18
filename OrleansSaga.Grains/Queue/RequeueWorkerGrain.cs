using System;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;

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
            catch(Exception ex)
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
                Log.Info($"Execute Command {commandId}");
                var delay = new Random().Next(100, 500);
                await Task.Delay(delay);
                //if (commandId % 7 == 0)
                //{
                //    throw new Exception("test");
                //}
                await requeueGrain.Complete(commandId, this);
                //await requeueGrain.Enqueue(commandId + 100000);
            }
            catch (Exception ex)
            {
                Log.Error(0, $"Worker execute Command {commandId} Exception: {ex}");
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
