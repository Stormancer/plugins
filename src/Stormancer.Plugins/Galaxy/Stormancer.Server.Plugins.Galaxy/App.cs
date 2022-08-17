using Stormancer.Plugins;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.Galaxy
{
    /// <summary>
    /// Plugin initialization class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Initialization method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new GogGalaxyPlugin());
        }
    }

    internal class GogGalaxyPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<GogGalaxyAuthenticationProvider>().As<IAuthenticationProvider>();
              };
        }
    }
}
