using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrleansSaga.Grains
{ 
    public class TaskHandler<TMessage, TResult> : TaskHandler<TMessage>
    {
        public TaskHandler(Func<Task<TMessage>, Task<TResult>> onCompleted = null, Func<Task<TMessage>, Task<TResult>> onFaulted = null, Func<Task<TMessage>, Task<TResult>> onCanceled = null)
            : base(t => onCompleted(t), t => onFaulted(t), t => onCanceled(t), typeof(TResult))
        {
            
        }

        public override Task Handle(Task task) => Handle(task as Task<TMessage>);

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


    public class TaskHandler<TMessage> : TaskHandler
    {
        public TaskHandler(Func<Task<TMessage>, Task> onSuccess = null, Func<Task<TMessage>, Task> onFaulted = null, Func<Task<TMessage>, Task> onCanceled = null, Type resultType = null)
            : base((Task t) => onSuccess(t as Task<TMessage>), (Task t) => onFaulted(t as Task<TMessage>), (Task t) => onCanceled(t as Task<TMessage>), typeof(TMessage), resultType)
        {
        }

        //public override Task Handle(Task task) => Handle(task as Task<TMessage>);

        //public virtual Task Handle(Task<TMessage> task) => base.Handle(task);

        //public virtual Task Handle(Task<TMessage> task)
        //{
        //    return task.ContinueWith(t =>
        //    {
        //        if (t.IsCompleted) return OnSuccess(t);
        //        if (t.IsFaulted) return OnFaulted(t);
        //        return OnCanceled(t);
        //    }).Unwrap();
        //}
    }

    public class TaskHandler
    {
        public Type MessageType { get; private set; }
        public Type ResultType { get; private set; }
        public Func<Task, Task> OnCompleted { get; private set; }
        public Func<Task, Task> OnFaulted { get; private set; }
        public Func<Task, Task> OnCanceled { get; private set; }

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
