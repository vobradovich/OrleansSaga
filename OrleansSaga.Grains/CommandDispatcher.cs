using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains
{
    public class CommandDispatcher<T> where T : class
    {
        public Func<Task, T> CommandHandler { get; private set; }
    }

    public class CommandDispatcher
    {        
        public Type CommandType { get; private set; }
    }
}
