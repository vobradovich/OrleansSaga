using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrleansSaga.Grains.Model;
using OrleansSaga.Grains;
using System.Threading;
using Orleans.TestingHost;

namespace OrleansSaga.Tests
{
    [TestFixture]
    public class RailwayGrainTest : TestingSiloHost, IDisposable
    {
        public void Dispose()
        {
            StopAllSilos();
        }

        [Test]
        public async Task SagaGrainTest1()
        {
            //IEventStore EventStore = new MemoryEventStore();
            //var grain = GrainFactory.GetGrain<IRailwayTestGrain>(0);
        }

        [Test]
        public async Task SagaGrainTest2()
        {
            IEventStore EventStore = new MemoryEventStore();
            var grain = new RailwayTestGrain(EventStore);

            await grain.OnActivateAsync();
            Console.WriteLine($"{DateTime.Now} Start");
            await grain.Start();
            Console.WriteLine($"{DateTime.Now} Delay");
            await Task.Delay(3000);
            Console.WriteLine($"{DateTime.Now} Done");
            Assert.Pass("SagaGrainTest2 Pass");
        }
    }
}
