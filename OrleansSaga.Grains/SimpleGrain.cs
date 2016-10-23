using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using OrleansSaga.Grains.Services;

namespace OrleansSaga.Grains
{
    public class SimpleGrain : Grain, ISimpleGrain, IRemindable
    {
        ISimpleService _simpleService;
        Logger _logger;

        public SimpleGrain(ISimpleService simpleService)
        {
            _simpleService = simpleService;            
        }

        public override Task OnActivateAsync()
        {
            _logger = GetLogger();
            return base.OnActivateAsync();
        }

        public Task Run(string s)
        {
            _logger.Info("Run");
            Task.Factory.StartNew(() => _simpleService.SetAsync(s))
                .Unwrap()
                .ContinueWith((prevTask) => _logger.Info($"SetAsync {prevTask.Status}"))
                .ContinueWith((prevTask) => _simpleService.GetAsync())
                .Unwrap()
                .ContinueWith((prevTask) => _logger.Info($"GetAsync {prevTask.Status} Result: {prevTask.Result}"))
                .ContinueWith((prevTask) => _simpleService.ThrowNotTen(10))
                .Unwrap()
                .ContinueWith((prevTask) => _logger.Info($"GetAsync {prevTask.Status}"));
            _logger.Info("Done");
            return TaskDone.Done;
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            throw new NotImplementedException();
        }
    }

    public interface ISimpleGrain : IGrainWithIntegerKey
    {
        Task Run(string s);
    }
}
