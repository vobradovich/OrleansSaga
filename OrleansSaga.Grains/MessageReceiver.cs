using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains
{
    public class MessageReceiver<TMessage> : MessageReceiver
    {
        public MessageReceiver(Func<Task<TMessage>, Task> receive = null, Func<Task<TMessage>, Task> apply = null)
            : base((Task t) => receive(t as Task<TMessage>), (Task t) => apply(t as Task<TMessage>), typeof(TMessage))
        {
        }
    }

    public class MessageReceiver
    {
        public Type MessageType { get; private set; }
        public Func<Task, Task> Receive { get; set; }
        public Func<Task, Task> Apply { get; set; }
        public TaskHandler Handler { get; set; }

        public Func<Task, Task> Skip = t => t;

        public MessageReceiver(Func<Task, Task> receive = null, Func<Task, Task> apply = null, Type messageType = null)
        {
            Receive = receive ?? Skip;
            Apply = apply ?? Skip;
            MessageType = messageType;
        }
    }

    public static class MessageReceiverExtensions
    {
        public static MessageReceiver<TMessage> Handle<TMessage, TResult>(this MessageReceiver<TMessage> receiver, Func<TMessage, Task<TResult>> success, Func<Exception, Task<TResult>> error = null, Func<Task<TResult>> cancel = null)
        {
            receiver.Handler = new TaskHandler<TMessage, TResult>(t => success(t.Result), t => error(t.Exception), t => cancel()); ;
            return receiver;
        }
    }
}
