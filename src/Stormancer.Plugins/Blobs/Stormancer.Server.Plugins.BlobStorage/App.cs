using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Entry point class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point method of the plugin.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new BlobStoragePlugin());
        }
    }

    internal class BlobStoragePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<BlobStorage>().As<IBlobStorage>().InstancePerRequest();
            };
        }
    }
}
