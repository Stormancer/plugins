using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.BlobStorage
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new BlobStoragePlugin());
        }
    }

    internal class BlobStoragePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            
        }
    }
}
