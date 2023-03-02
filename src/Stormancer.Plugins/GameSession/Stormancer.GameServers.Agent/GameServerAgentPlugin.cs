using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class GameServerAgentPlugin : IClientPlugin
    {
        private readonly DockerAgentConfigurationOptions _options;
        private readonly IAgentController _controller;

        public GameServerAgentPlugin(DockerAgentConfigurationOptions options, IAgentController controller)
        {
            _options = options;
            _controller = controller;
        }
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.RegisterDependency<DockerAgentConfigurationOptions>(_options);
                client.DependencyResolver.RegisterDependency<IAgentController>(_controller);
                client.DependencyResolver.Register<AgentApi>((dr) => new AgentApi(), true);
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new DockerAgentAuthEventHandler(dr.Resolve<DockerAgentConfigurationOptions>()));

            };
        }
    }
}
