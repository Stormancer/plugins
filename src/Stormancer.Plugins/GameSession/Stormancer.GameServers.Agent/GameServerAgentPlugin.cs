using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
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
        private readonly AgentController _controller;

        public GameServerAgentPlugin(DockerAgentConfigurationOptions options, AgentController controller)
        {
            _options = options;
            _controller = controller;
        }
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.RegisterDependency(_options);
                client.DependencyResolver.RegisterDependency(_controller);
                client.DependencyResolver.Register((dr) => new AgentApi(), true);
                
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new DockerAgentAuthEventHandler(dr.Resolve<DockerAgentConfigurationOptions>()));

            };

            ctx.SceneCreated += (Scene scene) =>
            {
                if (scene.Id == "authenticator")
                {
                    var controller = scene.DependencyResolver.Resolve<AgentController>();

                    scene.AddProcedure("agent.getRunningContainers",async ctx => {
                        var args = ctx.ReadObject<GetRunningContainersParameters>();

                        ctx.SendValue<GetRunningContainersResponse>(await controller.GetRunningContainers(args));

                    });
                    scene.AddProcedure("agent.tryStartContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStartParameters>();

                        ctx.SendValue<ContainerStartResponse>(await controller.TryStartContainer(args));
                    });
                    scene.AddProcedure("agent.stopContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStopParameters>();

                        ctx.SendValue<ContainerStopResponse>(await controller.StopContainer(args));
                    });
                    scene.AddProcedure("agent.getLogs",async ctx => {
                        var args = ctx.ReadObject<GetContainerLogsParameters>();

                        ctx.SendValue(await controller.GetContainerLogs(args));
                    });
                }
            };
        }
    }
}
