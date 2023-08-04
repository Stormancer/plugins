
using Stormancer.Plugins;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkCorePlugin())
        }
    }

    internal class EntityFrameworkCorePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<DbContextAccessor>().InstancePerRequest();
               
            };
        }
    }
}