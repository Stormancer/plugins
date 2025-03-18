using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore.Npgsql
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new NpgsqlPlugin());
        }
    }

    internal class NpgsqlPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<NpgSQLConfigurator>().As< IDbContextLifecycleHandler>();
                builder.Register<NpgSQLConfiguratorState>().SingleInstance().AsSelf().As<IConfigurationChangedEventHandler>();

            };
        }
    }
}