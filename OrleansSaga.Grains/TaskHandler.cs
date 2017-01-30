using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrleansSaga.Grains
{
    public class TaskHandler<TMessage, TResult> : TaskHandler<TMessage> where TMessage : class
    {
        public TaskHandler(Func<Task<TMessage>, Task<TResult>> onCompleted = null, Func<Task<TMessage>, Task<TResult>> onFaulted = null, Func<Task<TMessage>, Task<TResult>> onCanceled = null)
            : base(t => onCompleted(t), t => onFaulted(t), t => onCanceled(t), typeof(TResult))
        {

        }

        public TaskHandler(Func<Task<TMessage>, Task<TResult>> handler = null)
            : this(handler, handler, handler)
        {

        }

        public override Task Handle(Task task)
        {
            if (task is Task<TMessage>)
            {
                return Handle(task as Task<TMessage>);
            }
            return Handle(task as Task<object>);
        }

        private Task<TResult> Handle(Task<object> task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    return Handle(Task.FromException<TMessage>(t.Exception));
                return Handle(Task.FromResult(t.Result as TMessage));
            }).Unwrap();
        }

        public Task<TResult> Handle(Task<TMessage> task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsCanceled)
                    return (OnCanceled(t) as Task<TResult>);

                if (t.IsFaulted)
                    return (OnFaulted(t) as Task<TResult>);

                return (OnCompleted(t) as Task<TResult>);
            }).Unwrap();
        }
    }


    public class TaskHandler<TMessage> : TaskHandler where TMessage : class
    {
        public TaskHandler(Func<Task<TMessage>, Task> onSuccess = null, Func<Task<TMessage>, Task> onFaulted = null, Func<Task<TMessage>, Task> onCanceled = null, Type resultType = null)
            : base((Task t) => onSuccess(t as Task<TMessage>), (Task t) => onFaulted(t as Task<TMessage>), (Task t) => onCanceled(t as Task<TMessage>), typeof(TMessage), resultType)
        {
        }

        public override Task Handle(Task task)
        {
            if (task is Task<TMessage>)
            {
                return Handle(task as Task<TMessage>);
            }
            return Handle(task as Task<object>);
        } 

        private Task Handle(Task<TMessage> task) => base.Handle(task);

        private Task Handle(Task<object> task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    return base.Handle(Task.FromException<TMessage>(t.Exception));
                return base.Handle(Task.FromResult(t.Result as TMessage));
            }).Unwrap();
        }
    }

    public class TaskHandler
    {
        public Type MessageType { get; private set; }
        public Type ResultType { get; private set; }
        public Func<Task, Task> OnCompleted { get; private set; }
        public Func<Task, Task> OnFaulted { get; private set; }
        public Func<Task, Task> OnCanceled { get; private set; }

        public TaskHandler(Func<Task, Task> hanlder, Type messageType = null, Type resultType = null)
            : this(hanlder, hanlder, hanlder, messageType, resultType)
        {
        }

        public TaskHandler(Func<Task, Task> onSuccess = null, Func<Task, Task> onFaulted = null, Func<Task, Task> onCanceled = null, Type messageType = null, Type resultType = null)
        {
            OnCompleted = onSuccess ?? Skip;
            OnFaulted = onFaulted ?? Skip;
            OnCanceled = onCanceled ?? Skip;
            MessageType = messageType;
            ResultType = resultType;
        }

        public Func<Task, Task> Skip = t => t;

        public virtual Task Handle(Task task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsCanceled)
                    return OnCanceled(t);

                if (t.IsFaulted)
                    return OnFaulted(t);

                return OnCompleted(t);
            }).Unwrap();
        }
    }
}
