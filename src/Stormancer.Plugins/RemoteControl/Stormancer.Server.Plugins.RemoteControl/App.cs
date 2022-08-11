using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.RemoteControl
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new RemoteControlPlugin());
        }
    }

    internal class RemoteControlPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            
        }
    }
}