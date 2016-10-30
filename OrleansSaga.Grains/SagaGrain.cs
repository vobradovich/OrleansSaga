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
    public class SagaGrain<T> : Grain<T>, ISagaGrain, IRemindable, IHandler, IHandler<CancelMessage>
    {
        Dictionary<Type, Func<Task, Func<Task>>> _handlers = new Dictionary<Type, Func<Task, Func<Task>>>();
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

            OnEvent<CancelMessage>(m => { return Handle(m); });

            Events = (await _eventStore.LoadEvents(this.GetPrimaryKeyLong())).ToList();
            await Replay(Events);
            await base.OnActivateAsync();
        }

        public Task Receive(Task incomingMessage)
        {
            return TaskDone.Done;
        }

        public Task Receive<TMessage>(Task<TMessage> message)
        {
            Func<Task, Func<Task>> handler = null;            
            if (_handlers.TryGetValue(typeof(TMessage), out handler))
            {
                return message.ContinueWith(handler, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            return TaskDone.Done;
        }

        public Task Handle<TMessage>(TMessage message)
        {
            return TaskDone.Done;
        }


        protected void OnEvent<TMessage>(Func<Task<TMessage>, Task> action) where TMessage : class
        {
            Func<Task, Func<Task>> handler = (Task t) => () => action(t as Task<TMessage>);
            _handlers.Add(typeof(TMessage), handler);
        }

        protected void OnEvent<TMessage, TResult>(Func<Task<TMessage>, Task<TResult>> action) where TMessage : class
        {
            Func<Task, Func<Task<TResult>>> handler = (Task t) => () => action(t as Task<TMessage>);
            _handlers.Add(typeof(TMessage), handler);
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
