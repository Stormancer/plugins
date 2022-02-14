using Stormancer.Plugins;
using System;

namespace Stormancer.Server.Plugins.SocketApi
{
    /// <summary>
    /// Plugin entrypoint
    /// </summary>
    public class App
    {
        /// <summary>
        /// Method called by the framework to initialize the plugin.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new SocketPlugin());
        }
    }

    internal class SocketPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            
        }
    }
}
