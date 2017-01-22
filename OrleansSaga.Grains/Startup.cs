using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrleansSaga.Grains.Model;
using OrleansSaga.Grains.Services;

namespace OrleansSaga.Grains
{
    public class Startup
    {
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ISimpleService, SimpleService>();
            services.AddSingleton<IEventStore, MemoryEventStore>();

            return services.BuildServiceProvider();
        }
    }
}
