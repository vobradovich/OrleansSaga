using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public class SagaGrain : Grain, ISagaGrain, IRemindable, IHandler, IHandler<CancelMessage>
    {
        Dictionary<string, Action> _handlers = new Dictionary<string, Action>();
        IEventStore _eventStore;
        public List<StateEvent> Events { get; set; }
        protected CancellationTokenSource _cancellationTokenSource;
        Logger Log;

        public SagaGrain(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public override async Task OnActivateAsync()
        {
            Log = GetLogger();
            _cancellationTokenSource = new CancellationTokenSource();
            Events = (await _eventStore.LoadEvents(this.GetPrimaryKeyLong())).ToList();
            await Replay(Events);
            await base.OnActivateAsync();
        }

        Task SaveEvents(IEnumerable<StateEvent> events)
        {
            Events.AddRange(events);
            return TaskDone.Done;
        }

        async Task Replay(IEnumerable<StateEvent> events)
        {
            await Apply(events.Select(e => e.GetData()));
        }

        async Task Apply(IEnumerable<object> events)
        {
            foreach (var @event in events)
                await Dispatch(@event);
        }

        Task Dispatch(object message)
        {

            return Handle((dynamic)message);
        }

        public Task Handle(object message)
        {
            return TaskDone.Done;
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            return TaskDone.Done;
        }

        public Task Handle(CancelMessage message)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }            
            return TaskDone.Done;
        }
    }
}
