using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Queries
{
    class ServiceQueriesPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<SearchEngine>().SingleInstance();
                  builder.Register<LuceneSearchProvider>().As<IServiceSearchProvider>().As<ILucene>().SingleInstance();
              };

            ctx.HostStarting += (IHost host) =>
              {
                  host.DependencyResolver.Resolve<SearchEngine>().Initialize();
              };
        }
    }
}
