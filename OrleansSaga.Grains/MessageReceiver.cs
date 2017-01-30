using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrleansSaga.Grains.Model;

namespace OrleansSaga.Grains
{
    public class MessageReceiver<TMessage> : MessageReceiver where TMessage : class
    {
        public MessageReceiver(TaskHandler<TMessage> receiver = null, TaskHandler<TMessage> applier = null)
            : base(receiver, applier, typeof(TMessage))
        {
        }
    }

    public class MessageReceiver
    {
        public Type MessageType { get; private set; }
        public TaskHandler Receiver { get; set; }
        public TaskHandler Applier { get; set; }
        public TaskHandler Handler { get; set; }
        public int TryCount { get; set; }
        public IBackoffProvider BackoffProvider { get; set; }
        public MessageReceiver(TaskHandler receiver = null, TaskHandler applier = null, Type messageType = null)
        {
            Receiver = receiver;
            Applier = applier;
            MessageType = messageType;
        }
    }

    public static class MessageReceiverExtensions
    {
        public static MessageReceiver<TMessage> Apply<TMessage>(this MessageReceiver<TMessage> receiver, Func<TMessage, Task> success, Func<Exception, Task> error = null, Func<Task> cancel = null) where TMessage : class
        {
            receiver.Applier = new TaskHandler<TMessage>(t => success(t.Result), t => error(t.Exception), t => cancel());
            return receiver;
        }

        public static MessageReceiver<TMessage> Handle<TMessage, TResult>(this MessageReceiver<TMessage> receiver, Func<TMessage, Task<TResult>> success, Func<Exception, Task<TResult>> error = null, Func<Task<TResult>> cancel = null) where TMessage : class
        {
            receiver.Handler = new TaskHandler<TMessage, TResult>(t => success(t.Result), t => error(t.Exception), t => cancel());
            return receiver;
        }

        public static MessageReceiver<TMessage> WithRetries<TMessage>(this MessageReceiver<TMessage> receiver, int tryCount, IBackoffProvider backoffProvider) where TMessage : class
        {
            receiver.TryCount = tryCount;
            receiver.BackoffProvider = backoffProvider;
            return receiver;
        }

        public static MessageReceiver<TMessage> WithRetries<TMessage>(this MessageReceiver<TMessage> receiver, int tryCount, TimeSpan delay) where TMessage : class
        {
            receiver.TryCount = tryCount;
            receiver.BackoffProvider = new FixedBackoff(delay);
            return receiver;
        }
    }
}
