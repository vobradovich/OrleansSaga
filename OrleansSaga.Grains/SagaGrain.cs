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
        Dictionary<Type, MessageReceiver> _receivers = new Dictionary<Type, MessageReceiver>();
        IEventStore _eventStore;
        public List<StateEvent> Events { get; set; }
        protected CancellationTokenSource _cancellationTokenSource;
        Logger Log;
        IEventStore EventStore = new MemoryEventStore();

        public SagaGrain(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public override async Task OnActivateAsync()
        {
            //Log = GetLogger();
            _cancellationTokenSource = new CancellationTokenSource();

            OnMessage<CancelMessage>().Handle(HandleCancel);
            OnMessage<SagaCanceled>();

            //Events = (await _eventStore.LoadEvents(this.GetPrimaryKeyLong())).ToList();
            Events = (await _eventStore.LoadEvents(0)).ToList();
            await Replay(Events);
            //await base.OnActivateAsync();
        }

        public async Task Receive<TMessage>(Task<TMessage> taskMessage)
        {
            await Receive(taskMessage, typeof(TMessage));
        }

        public async Task Receive(Task taskMessage, Type messageType)
        {
            MessageReceiver receiver;
            if (!_receivers.TryGetValue(messageType, out receiver))
            {
                throw new NotImplementedException();
            }
            await receiver.Receive(taskMessage);
            await receiver.Apply(taskMessage);
            if (receiver.Handler != null)
            {
                await Task.Factory.StartNew(() => Dispatch(taskMessage, receiver.Handler));
            }
        }

        public Task Dispatch(Task taskMessage, TaskHandler handler)
        {
            if (handler.ResultType == null)
            {
                return handler.Handle(taskMessage);
            }
            var resultEvent = EventStore.LoadEvents(0).Result.FirstOrDefault(e => e.EventType == handler.ResultType.FullName);
            if (resultEvent != null)
            {
                switch (resultEvent.TaskStatus)
                {
                    case TaskStatus.RanToCompletion:
                        return Receive(Task.FromResult(resultEvent.GetData()), handler.ResultType);
                    case TaskStatus.Faulted:
                        return Receive(Task.FromException(resultEvent.GetData<Exception>()), handler.ResultType);                        
                    case TaskStatus.Canceled:
                    default:
                        return Receive(Task.FromCanceled(_cancellationTokenSource.Token), handler.ResultType);
                        //result = Task.FromException(new Exception());
                        //result = Task.FromCanceled(cts.Token);
                }
            }
            else
            {
                var task = handler.Handle(taskMessage);
                return task.ContinueWith(t => Receive(t, handler.ResultType));
            }            
        }

        public Func<TMessage, Task<TResult>> WithRetries<TMessage, TResult>(Func<TMessage, Task<TResult>> action, int tryCount = int.MaxValue, IBackoffProvider backoffProvider = null)
        {
            var provider = backoffProvider ?? FixedBackoff.Zero;
            return m =>
            {
                var resolver = new TaskCompletionSource<TResult>();
                RegisterTimer(RetryCallback(m, action, 0, tryCount, backoffProvider), resolver, TimeSpan.Zero, TimeSpan.Zero);
                return resolver.Task;
            };
        }

        private Func<object, Task> RetryCallback<TMessage, TResult>(TMessage message, Func<TMessage, Task<TResult>> action, int current, int tryCount, IBackoffProvider backoffProvider)
        {
            return async (state) =>
            {
                var resolver = (TaskCompletionSource<TResult>)state;
                try
                {
                    var result = await action(message);
                    resolver.TrySetResult(result);
                }
                catch (Exception ex) when (current < tryCount - 1)
                {
                    TimeSpan delay = backoffProvider.Next(current);
                    RegisterTimer(RetryCallback(message, action, current + 1, tryCount, backoffProvider), resolver, delay, TimeSpan.Zero);
                }
                catch (Exception ex)
                {
                    resolver.TrySetException(ex);
                }
            };
        }

        public Task Handle<TMessage>(TMessage message)
        {
            return TaskDone.Done;
        }

        protected MessageReceiver<TMessage> OnMessage<TMessage>() where TMessage : class
        {
            MessageReceiver receiver;
            if (!_receivers.TryGetValue(typeof(TMessage), out receiver))
            {
                var storeHandler = new TaskHandler<TMessage>(t => EventStore.AddEvents(StateEvent.FromMessage(0, t.Result)), t => EventStore.AddEvents(StateEvent.FromException<TMessage>(0, t.Exception)), t => EventStore.AddEvents(StateEvent.FromCancel<TMessage>(0)));
                var logHandler = new TaskHandler<TMessage>(t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Result}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Exception}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t}")));
                receiver = new MessageReceiver<TMessage>(storeHandler.Handle, logHandler.Handle);
                _receivers.Add(typeof(TMessage), receiver);
            }
            return receiver as MessageReceiver<TMessage>;
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

        public virtual Task<SagaCanceled> HandleCancel(CancelMessage cancelMessage)
        {
            return Task.FromResult(new SagaCanceled());
        }
    }
}
