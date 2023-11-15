using Stormancer.Plugins;
using Stormancer.Server.Plugins.BlobStorage;

namespace Stormancer.Server.Plugins.Azure
{
    /// <summary>
    /// Entry point.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new AzurePlugin());
        }
    }

    internal class AzurePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<AzureBlobStorageBackend>().As<IBlobStorageBackend>().InstancePerRequest();
                builder.Register<ConfigCache>().SingleInstance();
            };
        }
    }
}
