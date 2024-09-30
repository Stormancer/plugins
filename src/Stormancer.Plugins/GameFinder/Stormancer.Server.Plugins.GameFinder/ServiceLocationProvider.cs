using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    class ServiceLocationProvider : IServiceLocatorProvider
    {
        public static ServiceLocationProvider Instance { get; } = new ServiceLocationProvider();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            switch(ctx.ServiceType)
            {
                case "stormancer.plugins.gamefinder":
                    ctx.SceneId = ctx.ServiceName;
                    return Task.CompletedTask;
                   
                default:
                    return Task.CompletedTask;
            }
        }
    }
}
