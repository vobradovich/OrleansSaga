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
    public class RailwayTest
    {
        Dictionary<Type, TaskHandler[]> _receiverHandlers = new Dictionary<Type, TaskHandler[]>();
        Dictionary<Type, TaskHandler> _handlers = new Dictionary<Type, TaskHandler>();
        CancellationTokenSource cts = new CancellationTokenSource();
        IEventStore EventStore = new MemoryEventStore();

        protected void OnMessage<TMessage>(Func<TMessage, Task> success, Func<Exception, Task> error = null, Func<Task> cancel = null) where TMessage : class
        {
            if (!_handlers.ContainsKey(typeof(TMessage)))
            {
                var handler = new TaskHandler<TMessage>(t => success(t.Result), t => error(t.Exception), t => cancel());
                _handlers.Add(typeof(TMessage), handler);
                var storeHandler = new TaskHandler<TMessage>(t => EventStore.AddEvents(StateEvent.FromMessage(0, t.Result)), t => EventStore.AddEvents(StateEvent.FromException<TMessage>(0, t.Exception)), t => EventStore.AddEvents(StateEvent.FromCancel<TMessage>(0)));
                var logHandler = new TaskHandler<TMessage>(t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Result}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Exception}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t}")));
                _receiverHandlers.Add(typeof(TMessage), new[] { logHandler, storeHandler });
            }
        }

        protected void OnMessage<TMessage, TResult>(Func<TMessage, Task<TResult>> success, Func<Exception, Task<TResult>> error = null, Func<Task<TResult>> cancel = null) where TMessage : class
        {
            if (!_handlers.ContainsKey(typeof(TMessage)))
            {
                var handler = new TaskHandler<TMessage, TResult>(t => success(t.Result), t => error(t.Exception), t => cancel());
                _handlers.Add(typeof(TMessage), handler);
                var storeHandler = new TaskHandler<TMessage>(t => EventStore.AddEvents(StateEvent.FromMessage(0, t.Result)), t => EventStore.AddEvents(StateEvent.FromException<TMessage>(0, t.Exception)), t => EventStore.AddEvents(StateEvent.FromCancel<TMessage>(0)));
                var logHandler = new TaskHandler<TMessage>(t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Result}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t.Exception}")), t => Task.Factory.StartNew(() => Console.WriteLine($"{DateTime.Now} {t}")));
                _receiverHandlers.Add(typeof(TMessage), new[] { logHandler, storeHandler });
            }
        }

        public async Task Receive<TMessage>(Task<TMessage> taskMessage)
        {
            //TaskHandler[] pre;
            //if (_preHandlers.TryGetValue(typeof(TMessage), out pre))
            //{
            //    foreach (var h in pre)
            //    {
            //        await h.Handle(taskMessage);
            //    }
            //}
            //await Task.Factory.StartNew(() => Dispatch(taskMessage));
            await Receive(taskMessage, typeof(TMessage));
        }

        public async Task Receive(Task taskMessage, Type messageType)
        {
            //return Receive((dynamic)taskMessage);
            TaskHandler[] receivers;
            if (_receiverHandlers.TryGetValue(messageType, out receivers))
            {
                foreach (var h in receivers)
                {
                    await h.Handle(taskMessage);
                }
            }
            await Task.Factory.StartNew(() => Dispatch(taskMessage, messageType));
        }

        public Task Dispatch<TMessage>(Task<TMessage> taskMessage)
        {
            return Dispatch(taskMessage, typeof(TMessage));
        }

        public Task Dispatch(Task taskMessage, Type messageType)
        {            
            TaskHandler handler;
            if (!_handlers.TryGetValue(messageType, out handler))
            {
                return Task.FromException(new NotImplementedException());
            }

            if (handler.ResultType != null)
            {
                Task result;
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
                            return Receive(Task.FromCanceled(cts.Token), handler.ResultType);
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
            return handler.Handle(taskMessage);
        }

        //public Task<TResult> Receive<TMessage, TResult>(Task<TMessage> taskMessage)
        //{
        //    TaskHandler handler;
        //    if (_handlers.TryGetValue(typeof(TMessage), out handler))
        //    {
        //        return handler.Handle(taskMessage) as Task<TResult>;
        //    }
        //    return Task.FromException<TResult>(new NotImplementedException());
        //}

        public async Task Send<TMessage>(TMessage message)
        {
        }

        [Test]
        public async Task SagaTest1()
        {
            OnMessage<CancelMessage>(m => Handle(m));
            await Receive(Task.FromResult(new CancelMessage("TEST")));
            Console.WriteLine($"{DateTime.Now} Done");
            // TODO: Add your test code here
            Assert.Pass("Your first passing test");
        }

        [Test]
        public async Task SagaTest2()
        {
            OnMessage<StartMessage, DoneMessage>(m => Handle(m));
            OnMessage<DoneMessage>(m => Handle(m));
            Console.WriteLine($"{DateTime.Now} Start");
            await Receive(Task.FromResult(new StartMessage()));
            Console.WriteLine($"{DateTime.Now} Delay");
            await Task.Delay(3000);
            // TODO: Add your test code here
            Console.WriteLine($"{DateTime.Now} Done");
            var events = await EventStore.LoadEvents(0);
            Assert.Pass("Your first passing test");
        }

        [Test]
        public async Task SagaTestThrowException()
        {
            OnMessage<StartMessage, DoneMessage>(m => HandleThrow(m));
            OnMessage<DoneMessage>(m => Handle(m), ex => HandleException(ex));
            Console.WriteLine($"{DateTime.Now} Start");
            await Receive(Task.FromResult(new StartMessage()));
            Console.WriteLine($"{DateTime.Now} Delay");
            await Task.Delay(3000);
            // TODO: Add your test code here
            Console.WriteLine($"{DateTime.Now} Done");
            var events = await EventStore.LoadEvents(0);
            Assert.Pass("Your first passing test");
        }

        [Test]
        public async Task SagaTestCancel()
        {
            OnMessage<StartMessage, DoneMessage>(m => Handle(m));
            OnMessage<DoneMessage>(m => Handle(m), ex => HandleException(ex), () => HandleCanceled());
            OnMessage<CancelMessage>(Handle);
            Console.WriteLine($"{DateTime.Now} Start");
            await Receive(Task.FromResult(new StartMessage()));
            Console.WriteLine($"{DateTime.Now} Delay");
            await Task.Delay(100);
            await Receive(Task.FromResult(new CancelMessage("Cancel!")));
            await Task.Delay(3000);
            // TODO: Add your test code here
            Console.WriteLine($"{DateTime.Now} Done");
            var events = await EventStore.LoadEvents(0);
            Assert.Pass("Your first passing test");
        }

        public async Task<DoneMessage> Handle(StartMessage message)
        {
            Console.WriteLine($"{DateTime.Now} Handle StartMessage");
            await Task.Delay(2000, cts.Token);
            return new DoneMessage();
        }

        public async Task Handle(DoneMessage message)
        {
            Console.WriteLine($"{DateTime.Now} Handle DoneMessage");
            await Task.Delay(2000, cts.Token);
        }

        public async Task<DoneMessage> HandleThrow(StartMessage message)
        {
            Console.WriteLine($"{DateTime.Now} HandleThrow DoneMessage");
            await Task.Delay(1000);
            throw new Exception();
        }

        public async Task HandleException(Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} HandleException {ex}");
            await Task.Delay(1000);
        }

        public async Task HandleCanceled()
        {
            Console.WriteLine($"{DateTime.Now} HandleCanceled");
            await Task.Delay(1000);
        }

        public Task Handle(CancelMessage message)
        {
            cts.Cancel();
            Console.WriteLine($"{DateTime.Now} Handle CancelMessage: {message.Reason}");
            return Task.Delay(1000);
        }
    }

    public class StartMessage
    {
    }

    public class DoneMessage
    {
    }
}
