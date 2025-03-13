
using Stormancer.Diagnostics;
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
           
                builder.Register(static r=> new DbContextAccessor(new DbContextEventHandlers(r.ResolveAll<IDbModelBuilder>(), r.ResolveAll<IDbContextLifecycleHandler>()),r.Resolve<ILogger>())).InstancePerRequest();
                builder.Register(static r=> new AdminWebApiConfig()).As<IAdminWebApiConfig>();
            };
        }
    }
}