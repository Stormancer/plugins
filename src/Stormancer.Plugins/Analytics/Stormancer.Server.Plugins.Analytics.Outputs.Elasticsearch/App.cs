using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Analytics.Outputs.Elasticsearch
{
    /// <summary>
    /// Plugin entry point class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Plugin entry point.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ElasticAnalyticsOutputPlugin());
        }
    }

    class ElasticAnalyticsOutputPlugin : IHostPlugin
    {
     
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                
                builder.Register(static r => new ElasticsearchOutput(r.Resolve<IESClientFactory>(), r.Resolve<IConfiguration>(), r.Resolve<ILogger>())).As<IAnalyticsOutput>();
               
            };
            
        }
    }

 
}
