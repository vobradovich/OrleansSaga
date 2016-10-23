using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Model
{
    public interface IHandler<T> : IHandler
    {
        Task Handle(T message);
    }

    public interface IHandler
    {
        Task Handle(object message);
    }
}
