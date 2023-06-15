using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession;

namespace Stormancer.Server.Plugins.Gameye
{
    public class App
    {
        public void Run(IAppBuilder builder)
        { 
            builder.AddPlugin(new GameyePlugin());
        }
    }


    public class GameyePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<GameyeClient>().SingleInstance();
                builder.Register<GameyeServerProvider>().As<IGameServerProvider>();
            };
        }
    }
}