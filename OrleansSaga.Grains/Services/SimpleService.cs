using System;
using System.Threading.Tasks;

namespace OrleansSaga.Grains.Services
{
    public class SimpleService : ISimpleService
    {
        private string _s;

        public async Task SetAsync(string s)
        {
            await Task.Delay(10000);
            Console.WriteLine($"{DateTime.Now} SimpleService.SetAsync {s}");
            _s = s;            
        }

        public async Task<string> GetAsync()
        {
            await Task.Delay(1000);
            Console.WriteLine($"{DateTime.Now} SimpleService.GetAsync {_s}");
            return _s;
        }

        public async Task<int> ThrowNotTen(int i)
        {
            await Task.Delay(100);
            Console.WriteLine($"{DateTime.Now} SimpleService.ThrowNotTen {i}");
            if (i != 10)
            {
                throw new Exception("i != 10");
            }
            return i;
        }
    }

    public interface ISimpleService
    {
        Task<string> GetAsync();

        Task SetAsync(string s);

        Task<int> ThrowNotTen(int i);
    }
}
