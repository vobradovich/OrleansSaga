using System;
using System.Linq;
using System.Threading.Tasks;
using Orleans.Providers;
using OrleansSaga.Grains.Queue;

namespace OrleansSaga.Grains
{
    public class Bootstrap : IBootstrapProvider
    {
        public string Name
        {
            get
            {
                return "OrleansSaga";
            }
        }

        public Task Close()
        {
            return Task.CompletedTask;
        }

        public async Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            //var simpleGrain = providerRuntime.GrainFactory.GetGrain<ISimpleGrain>(0);
            //simpleGrain.Run("TEST!!!!!!");

            //var simpleGrain = providerRuntime.GrainFactory.GetGrain<IManagerGrain>(0);
            //simpleGrain.Run(50, 100000);

            //for (int i = 0; i < 1; i++)
            //{
            //    var railwayTestGrain = providerRuntime.GrainFactory.GetGrain<IRailwayTestGrain>(i);
            //    railwayTestGrain.Start();

            //}            

            //var queueGrain = providerRuntime.GrainFactory.GetGrain<ICommandQueueGrain>(Guid.Empty);
            //await queueGrain.Register(typeof(StartMessage), o => {
            //    return TaskDone.Done;
            //});
            //await queueGrain.Register(typeof(ProgressMessage), o => {
            //    return TaskDone.Done;
            //});
            //await queueGrain.Register(typeof(DoneMessage), o => {
            //    return TaskDone.Done;
            //});
            //await queueGrain.Start(TimeSpan.FromSeconds(1));
            //await queueGrain.Add(new StartMessage());

            DateTimeOffset offset = DateTimeOffset.Now.AddSeconds(15);

            for (int q = 0; q < 100; q++)
            {
                var seed = new Random().Next(10000, int.MaxValue);
                var queueGrain = providerRuntime.GrainFactory.GetGrain<IRequeueGrain>($"TestQueue{q}");
                var commandids = Enumerable.Range(0, 1 * 100).Select(i => (long)i + seed).ToArray();
                await queueGrain.Schedule(offset, commandids);
            }

            //return TaskDone.Done;
        }
    }
}
