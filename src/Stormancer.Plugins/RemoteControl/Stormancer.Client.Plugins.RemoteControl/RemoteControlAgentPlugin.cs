using Stormancer.Plugins;

namespace Stormancer.Plugins.RemoteControl
{
    public class RemoteControlAgentPlugin : IClientPlugin
    {
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.Register(dr => new RemoteControlAgentApi(dr.Resolve<UserApi>()), true);
                client.DependencyResolver.Register(dr => new RemoteControlConfiguration(), true);
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new RemoteControlledAgentAuthEventHandler(dr.Resolve<RemoteControlConfiguration>()));
            };
        }
    }
}