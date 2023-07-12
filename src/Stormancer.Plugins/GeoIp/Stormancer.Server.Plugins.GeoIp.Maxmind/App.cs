using Stormancer.Plugins;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.GeoIp.Maxmind
{
    public class App
    {
        public void Run(IAppBuilder app) 
        {
            app.AddPlugin(new GeoIpPlugin());
        }
    }

    internal class GeoIpPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<MaxMindGeoIpService>().As<IGeoIpService>().SingleInstance();
                builder.Register<UserSessionEventHandler>().As<IUserSessionEventHandler>().InstancePerRequest();
            };
        }
    }
}