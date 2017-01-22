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
    public class RailwayGrain : Grain, ISagaGrain, IRemindable
    {
        protected Dictionary<Type, MessageReceiver> _receivers = new Dictionary<Type, MessageReceiver>();
        protected CancellationTokenSource CancellationTokenSource { get; set; }
        protected Logger Log { get; set; }
        protected IEventStore EventStore { get; set; }

        public RailwayGrain(IEventStore eventStore)
        {
            EventStore = eventStore;
        }

        public override async Task OnActivateAsync()
        {
            OnMessage<CancelMessage>().Handle(HandleCancel);
            OnMessage<SagaCanceled>();

            Log = GetLogger();
            CancellationTokenSource = new CancellationTokenSource();

            var grainId = this.GetPrimaryKeyLong();
            var events = (await EventStore.LoadEvents(grainId)).ToList();
            var firstEvent = events.FirstOrDefault();
            if (firstEvent != null)
            {
                await Replay(firstEvent);
            }
            await base.OnActivateAsync();
        }

        public async Task Receive<TMessage>(Task<TMessage> taskMessage)
        {
            await Receive(taskMessage, typeof(TMessage));
        }

        public async Task Receive(Task taskMessage, Type messageType)
        {
            var grainId = this.GetPrimaryKeyLong();
            Console.WriteLine($"{DateTime.Now} {grainId} Receive {messageType}");
            MessageReceiver receiver;
            if (!_receivers.TryGetValue(messageType, out receiver))
            {
                throw new NotImplementedException();
            }
            
            var events = await EventStore.LoadEvents(grainId);
            if (receiver.Receiver !=null && !events.Any(e => e.EventType == messageType.FullName))
            {
                await receiver.Receiver.Handle(taskMessage);
            }            
            if (receiver.Applier != null)
            {
                await receiver.Applier.Handle(taskMessage);
            }            
            await Task.Factory.StartNew(() => Dispatch(taskMessage, receiver, events));
        }

        public Task Dispatch(Task taskMessage, MessageReceiver receiver, IEnumerable<StateEvent> events)
        {
            if (receiver.Handler == null)
            {
                return TaskDone.Done;
            }
            if (receiver.Handler.ResultType == null)
            {
                return receiver.Handler.Handle(taskMessage);
            }
            var resultEvent = events.FirstOrDefault(e => e.EventType == receiver.Handler.ResultType.FullName);
            if (resultEvent != null)
            {                
                return Replay(resultEvent);
            }
            else
            {
                return Retry(taskMessage, receiver);
                //var task = handler.Handle(taskMessage);
                //return task.ContinueWith(t => Receive(t, handler.ResultType)).Unwrap();
            }
        }

        public Task Retry(Task taskMessage, MessageReceiver receiver, int current = 0)
        {
            var backoffProvider = receiver.BackoffProvider ?? FixedBackoff.Zero;
            var delay = current == 0 ? TimeSpan.Zero : backoffProvider.Next(current);
            var callback = RetryCallback(taskMessage, current);
            Console.WriteLine($"{DateTime.Now} RegisterTimer {current} {receiver.MessageType} {delay}");
            RegisterTimer(callback, receiver, delay, TimeSpan.FromDays(10));
            return TaskDone.Done;
        }

        private Func<object, Task> RetryCallback(Task taskMessage, int current)
        {
            return (state) =>
            {
                var receiver = state as MessageReceiver;
                var task = receiver.Handler.Handle(taskMessage);
                return task.ContinueWith(t =>
                {
                    if (t.IsFaulted && current < receiver.TryCount)
                    {
                        var ex = t.Exception;
                        Console.WriteLine($"{DateTime.Now} Retry {current} {receiver.MessageType} {ex}");
                        return Retry(taskMessage, receiver, current + 1);
                    }
                    return Receive(t, receiver.Handler.ResultType);
                }).Unwrap();
            };
        }

        //public Task Retry<TResult>(Task taskMessage, MessageReceiver receiver)
        //{
        //    var backoffProvider = receiver.BackoffProvider ?? FixedBackoff.Zero;
        //    TimeSpan delay = backoffProvider.Next(0);
        //    var resolver = new TaskCompletionSource<TResult>();
        //    var callback = RetryCallback(taskMessage, receiver.Handler.Handle, 0, receiver.TryCount, backoffProvider);
        //    RegisterTimer(callback, resolver, delay, TimeSpan.Zero);
        //    return resolver.Task;
        //}

        //private Func<object, Task> RetryCallback<TResult>(Task taskMessage, Func<Task, Task<TResult>> action, int current, int tryCount, IBackoffProvider backoffProvider)
        //{
        //    return async (state) =>
        //    {
        //        var resolver = (TaskCompletionSource<TResult>)state;
        //        try
        //        {
        //            var result = await action(taskMessage);
        //            resolver.TrySetResult(result);
        //        }
        //        catch (Exception ex) when (current < tryCount - 1)
        //        {
        //            TimeSpan delay = backoffProvider.Next(current);
        //            RegisterTimer(RetryCallback(taskMessage, action, current + 1, tryCount, backoffProvider), resolver, delay, TimeSpan.Zero);
        //        }
        //        catch (Exception ex)
        //        {
        //            resolver.TrySetException(ex);
        //        }
        //    };
        //}

        protected MessageReceiver<TMessage> OnMessage<TMessage>() where TMessage : class
        {
            var grainId = this.GetPrimaryKeyLong();
            MessageReceiver receiver;
            if (!_receivers.TryGetValue(typeof(TMessage), out receiver))
            {
                var storeHandler = new TaskHandler<TMessage>(t => EventStore.AddEvents(StateEvent.FromMessage(grainId, t.Result)), t => EventStore.AddEvents(StateEvent.FromException<TMessage>(grainId, t.Exception)), t => EventStore.AddEvents(StateEvent.FromCancel<TMessage>(grainId)));
                //var logHandler = new TaskHandler<TMessage>(t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Result}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Exception}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t}")));
                receiver = new MessageReceiver<TMessage>(storeHandler, null);
                _receivers.Add(typeof(TMessage), receiver);
            }
            return receiver as MessageReceiver<TMessage>;
        }

        Task Replay(StateEvent resultEvent)
        {
            Console.WriteLine($"{DateTime.Now} Replay {resultEvent.EventType}");
            var type = Type.GetType(resultEvent.EventType);
            switch (resultEvent.TaskStatus)
            {
                case TaskStatus.Canceled:
                    return Receive(Task.FromCanceled(CancellationTokenSource.Token), type);
                case TaskStatus.Faulted:
                    return Receive(Task.FromException(resultEvent.GetData<Exception>()), type);
                case TaskStatus.RanToCompletion:
                default:
                    return Receive(Task.FromResult(resultEvent.GetData()), type);
            }
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            return TaskDone.Done;
        }

        public virtual Task<SagaCanceled> HandleCancel(CancelMessage cancelMessage)
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                CancellationTokenSource.Cancel();
            }
            return Task.FromResult(new SagaCanceled());
        }
    }
}
