using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Orleans;

namespace OrleansSaga.Grains
{
    public class PingGrain : Grain, IPingGrain
    {
        IManagerGrain _manager;
        IPongGrain _grain;
        int _repeats;
        int _current = 0;
        public override Task OnActivateAsync()
        {
            _grain = GrainFactory.GetGrain<IPongGrain>(this.GetPrimaryKeyLong());
            return base.OnActivateAsync();
        }
        public Task Ping(int i)
        {
            _current++;
            if (_current < _repeats)
            {
                Task.Factory.StartNew(() => _grain.Pong(this, _current));
            }
            else
            {
                Task.Factory.StartNew(() => _manager.Finish(this.GetPrimaryKeyLong()));
            }            
            return TaskDone.Done;
        }

        public Task Run(IManagerGrain manager, int repeat)
        {
            _manager = manager;
            _repeats = repeat;
            return Ping(0);
        }
    }

    public interface IPingGrain : IGrainWithIntegerKey
    {
        Task Run(IManagerGrain manager, int repeat);
        Task Ping(int i);
    }

    public class PongGrain : Grain, IPongGrain
    {
        public Task Pong(IPingGrain grain, int i)
        {
            //if (i % 100 == 0)
            //{
            //    Console.WriteLine($"Pong {this.GetPrimaryKeyLong()} {i}");
            //}
            Task.Factory.StartNew(() => grain.Ping(i));
            return TaskDone.Done;
        }
    }

    public interface IPongGrain : IGrainWithIntegerKey
    {
        Task Pong(IPingGrain grain, int i);
    }

    public class ManagerGrain : Grain, IManagerGrain
    {
        Stopwatch sw = new Stopwatch();
        int _grains;
        int _repeat;
        List<long> _finished = new List<long>();

        public Task Finish(long grain)
        {
            _finished.Add(grain);
            if (_finished.Count == _grains)
            {
                sw.Stop();
                var totalMessagesReceived = _repeat * _grains * 2; // communication in Orleans' is always two-way
                var throughput = (totalMessagesReceived / sw.Elapsed.TotalSeconds);
                Console.WriteLine($"Finished {_grains} Grains. Elapsed: {sw.Elapsed}. Total messages: {totalMessagesReceived}. Throughput: {throughput}");

            }
            return TaskDone.Done;
        }

        public Task Run(int grains, int repeat)
        {
            sw.Start();
            Console.WriteLine($"Start {grains} Grains. Repeat: {repeat}");
            _grains = grains;
            _repeat = repeat;
            var tasks = Enumerable.Range(1, grains)
                .Select(i => GrainFactory.GetGrain<IPingGrain>(i))
                .Select(g => g.Run(this, repeat));
            return Task.WhenAll(tasks);
        }
    }

    public interface IManagerGrain : IGrainWithIntegerKey
    {
        Task Run(int grains, int repeat);
        Task Finish(long grain);
    }
}
