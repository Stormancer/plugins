using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.Azure
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new AzurePlugin());
        }
    }

    internal class AzurePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
           
        }
    }
}
