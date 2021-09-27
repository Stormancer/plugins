using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceBrowser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.ServiceQueries
{
    class ServiceQueriesPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<ServiceSearchEngine>().SingleInstance();
              };

            ctx.HostStarting += (IHost host) =>
              {
                  host.DependencyResolver.Resolve<ServiceSearchEngine>().Initialize();
              };
        }
    }
}
