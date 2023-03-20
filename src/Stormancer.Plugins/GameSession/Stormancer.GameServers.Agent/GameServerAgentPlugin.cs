using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.CodeDom;
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
        private readonly DockerService _docker;

        public GameServerAgentPlugin(DockerAgentConfigurationOptions options, AgentController controller, DockerService docker)
        {
            _options = options;
            _controller = controller;
            this._docker = docker;
        }
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.RegisterDependency(_options);
                client.DependencyResolver.RegisterDependency(_controller);
                client.DependencyResolver.Register((dr) => new AgentApi(dr.Resolve<DockerService>(),dr.Resolve<UserApi>(), dr.Resolve<Stormancer.Diagnostics.ILogger>()), true);
                client.DependencyResolver.RegisterDependency(_docker);
                
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new DockerAgentAuthEventHandler(dr.Resolve<DockerAgentConfigurationOptions>()));

            };

            ctx.SceneCreated += (Scene scene) =>
            {
                if (scene.Id == "authenticator")
                {
                    var controller = scene.DependencyResolver.Resolve<AgentController>();

                    scene.AddProcedure("agent.getRunningContainers",async ctx => {
                        var args = ctx.ReadObject<GetRunningContainersParameters>();

                        ctx.SendValue(await controller.GetRunningContainers(args));

                    });

                    scene.AddProcedure("agent.tryStartContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStartParameters>();

                        ctx.SendValue(await controller.TryStartContainer(args));
                    });

                    scene.AddProcedure("agent.stopContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStopParameters>();

                        ctx.SendValue(await controller.StopContainer(args));
                    });

                    scene.AddProcedure("agent.getLogs",async ctx => {
                        var args = ctx.ReadObject<GetContainerLogsParameters>();
                        await foreach(var block in controller.GetContainerLogs(args, ctx.CancellationToken))
                        {
                            ctx.SendValue(block);
                        }
                    });

                    scene.AddProcedure("agent.getDockerEvents", async ctx =>
                    {
                        await foreach(var evt in controller.SubscribeToContainerUpdates(ctx.CancellationToken))
                        {
                            ctx.SendValue(evt);
                        }
                    });

                    scene.AddProcedure("agent.getContainerStats", async ctx =>
                    {
                        var args = ctx.ReadObject<GetContainerStatsParameters>();

                        await foreach (var stat in controller.GetContainerStats(args, ctx.CancellationToken))
                        {
                            ctx.SendValue(stat);
                        }
                    });
                    scene.AddProcedure("agent.getStatus", async ctx =>
                    {
                        var status = await controller.GetAgentStatus();
                        ctx.SendValue(status);
                    });
                }
            };
        }
    }
}
