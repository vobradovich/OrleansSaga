using System;
using System.Threading.Tasks;
using Orleans;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    /// <summary>
    /// Grain implementation class SagaTestGrain.
    /// </summary>
    public class RailwayTestGrain : RailwayGrain, IRailwayTestGrain
    {
        public RailwayTestGrain(IEventStore eventStore) : base(eventStore)
        {
        }

        public override Task OnActivateAsync()
        {
            OnMessage<StartMessage>().Handle(HandleThrow).WithRetries(10, new FibonacciBackoff(TimeSpan.FromMilliseconds(100)));
            OnMessage<ProgressMessage>().Handle(Handle, HandleException);
            OnMessage<DoneMessage>().Apply(Apply);
            OnMessage<ErrorMessage>().Apply(ApplyError);

            return base.OnActivateAsync();
        }

        public async Task ApplyError(ErrorMessage arg)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} ApplyError");
        }

        public Task Start()
        {
            return Receive(Task.FromResult(new StartMessage()));
        }

        public async Task<ProgressMessage> Handle(StartMessage message)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} Handle StartMessage");
            await Task.Delay(1000, CancellationTokenSource.Token);
            return new ProgressMessage();
        }

        public async Task<DoneMessage> Handle(ProgressMessage message)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} Handle ProgressMessage");
            await Task.Delay(10000, CancellationTokenSource.Token);
            return new DoneMessage();
        }

        public async Task<ProgressMessage> HandleThrow(StartMessage message)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} HandleThrow StartMessage");
            await Task.Delay(1000);
            throw new Exception();
        }

        public async Task<DoneMessage> HandleException(Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} HandleException {ex}");
            await Task.Delay(1000);
            return new DoneMessage();
        }

        public async Task<DoneMessage> HandleExceptionAndThrow(Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} HandleExceptionAndThrow {ex}");
            await Task.Delay(1000);
            throw ex;
        }
         
        public async Task Apply(DoneMessage message)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} Apply DoneMessage");
        }
    }

    public interface IRailwayTestGrain : IGrainWithIntegerKey
    {
        Task Start();
    }

    public class StartMessage
    {
    }

    public class ProgressMessage
    {
    }

    public class DoneMessage
    {
    }

    public class ErrorMessage
    {
    }
}
