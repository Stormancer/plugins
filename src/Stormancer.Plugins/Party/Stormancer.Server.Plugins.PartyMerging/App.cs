using Stormancer.Plugins;
using Stormancer.Server.Plugins.PartyMerging;

namespace Stormancer.Server.Plugins.PartyFinder
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new PartyMergingPlugin());
        }
    }

    internal class PartyMergingPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<PartyMergingService>().InstancePerScene();
                builder.Register<PartyMergerController>();

            };
        }
    }
}
