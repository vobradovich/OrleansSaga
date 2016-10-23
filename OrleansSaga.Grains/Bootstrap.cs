using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;
using Orleans.Providers;

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
            return TaskDone.Done;
        }

        public Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            //var simpleGrain = providerRuntime.GrainFactory.GetGrain<ISimpleGrain>(0);
            //simpleGrain.Run("TEST!!!!!!");

            var simpleGrain = providerRuntime.GrainFactory.GetGrain<IManagerGrain>(0);
            simpleGrain.Run(50, 100000);

            return TaskDone.Done;
        }
    }
}
