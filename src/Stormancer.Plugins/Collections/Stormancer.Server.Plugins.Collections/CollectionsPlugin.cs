using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    internal class CollectionsPlugin : IHostPlugin
    {
        public const string SERVICE_ID = "stormancer.collections";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<CollectionsController>().InstancePerRequest();
                builder.Register<CollectionsService>().InstancePerRequest();
                builder.Register<CollectionsRepository>().SingleInstance();
                builder.Register<CollectionProfilePartBuilder>().InstancePerRequest();
            };
        }
    }

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
            builder.AddPlugin(new CollectionsPlugin());
        }
    }
}
