using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrleansSaga.Grains.Model;
using OrleansSaga.Grains;
using System.Threading;

namespace OrleansSaga.Tests
{
    [TestFixture]
    public class SagaGrainTest
    {
        [Test]
        public async Task SagaGrainTest1()
        {
            IEventStore EventStore = new MemoryEventStore();
            SagaGrain grain = new SagaGrain(EventStore);

            await grain.OnActivateAsync();

            await grain.Receive(Task.FromResult(new CancelMessage("TEST")));
        }
    }
}
