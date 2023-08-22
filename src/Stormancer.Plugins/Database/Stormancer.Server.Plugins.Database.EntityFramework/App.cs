
using Stormancer.Plugins;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkCorePlugin());
        }
    }

    internal class EntityFrameworkCorePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<DbContextAccessor>().InstancePerRequest();
                builder.Register<AdminWebApiConfig>().As<IAdminWebApiConfig>();
            };
        }
    }
}