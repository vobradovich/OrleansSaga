using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Streams;

namespace OrleansSaga.Grains
{
    public class CommandDispatcher<T> where T : class
    {
        public Func<Task, T> CommandHandler { get; private set; }
    }

    public abstract class CommandDispatcher
    {        
        public Type CommandType { get; private set; }
        public abstract Task Dispatch(object command, Type commandType);
    }

    public class StreamCommandDispatcher<T> : CommandDispatcher where T : class
    {
        private IAsyncObserver<T> _observer;
        public StreamCommandDispatcher(IAsyncObserver<T> observer)
        {
            _observer = observer;
        }
        public override Task Dispatch(object command, Type commandType)
        {
            return _observer.OnNextAsync(command as T);
        }
    }
}
