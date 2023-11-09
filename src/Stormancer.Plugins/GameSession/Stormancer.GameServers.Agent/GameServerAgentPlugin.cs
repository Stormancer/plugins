using Stormancer.Networking;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ClientsManager _clientsManager;

        public GameServerAgentPlugin(DockerAgentConfigurationOptions options,AgentController controller, DockerService docker, ClientsManager clientsManager)
        {
            _options = options;
            _controller = controller;
            this._docker = docker;
            _clientsManager = clientsManager;
        }
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
               
                client.DependencyResolver.RegisterDependency(_options);
                client.DependencyResolver.RegisterDependency(_controller);
                client.DependencyResolver.Register((dr) => new AgentApi(client,dr.Resolve<UserApi>(), dr.Resolve<Stormancer.Diagnostics.ILogger>()), true);
                client.DependencyResolver.RegisterDependency(_docker);
                
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new DockerAgentAuthEventHandler(dr.Resolve<DockerAgentConfigurationOptions>(),dr.Resolve<AgentApi>()));

            };
            ctx.ClientDisconnecting += (client) =>
            {
                AgentApi? api = client.DependencyResolver.Resolve<AgentApi>();
              
                _clientsManager.RemoveClient(api.AgentGuid);
            };

            ctx.SceneCreated += (Scene scene) =>
            {
                if (scene.Id == "gamesession-serverpool")
                {
                    var controller = scene.DependencyResolver.Resolve<AgentController>();
                    var api = scene.DependencyResolver.Resolve<AgentApi>();
                    api.ServerPoolsScene = scene;
                    controller.UserApi = scene.DependencyResolver.Resolve<UserApi>();
                    scene.AddProcedure("agent.getRunningContainers",async ctx => {
                        var args = ctx.ReadObject<bool>();

                        ctx.SendValue(await controller.GetRunningContainers(api.AgentGuid,new GetRunningContainersParameters()));

                    });

                    scene.AddProcedure("agent.tryStartContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStartParameters>();

                        ctx.SendValue(await controller.TryStartContainer(api.AgentGuid, args,ctx.CancellationToken));
                    });

                    scene.AddProcedure("agent.stopContainer", async ctx => {
                        var args = ctx.ReadObject<ContainerStopParameters>();

                        ctx.SendValue(await controller.StopContainer(api.AgentGuid, args));
                    });

                    scene.AddProcedure("agent.getLogs",async ctx => {
                        var args = ctx.ReadObject<GetContainerLogsParameters>();
                        await foreach(var block in controller.GetContainerLogs(api.AgentGuid, args, ctx.CancellationToken))
                        {
                            ctx.SendValue(block);
                        }
                    });

                    scene.AddProcedure("agent.getDockerEvents", async ctx =>
                    {
                        await foreach(var evt in controller.SubscribeToContainerUpdates(api.AgentGuid, ctx.CancellationToken))
                        {
                            ctx.SendValue(evt);
                        }
                    });

                    scene.AddProcedure("agent.getContainerStats", async ctx =>
                    {
                        var args = ctx.ReadObject<GetContainerStatsParameters>();

                        await foreach (var stat in controller.GetContainerStats(api.AgentGuid, args, ctx.CancellationToken))
                        {
                            ctx.SendValue(stat);
                        }
                    });
                    scene.AddProcedure("agent.getStatus", async ctx =>
                    {
                        var status = await controller.GetAgentStatus();
                        ctx.SendValue(status);
                    });

                    scene.AddRoute("agent.UpdateActiveApp", p => {
                        var activeDeploymentId = p.ReadObject<string>();
                        _clientsManager.AppDeploymentUpdated(api.AgentGuid, activeDeploymentId);
                    });
                }
            };
        }
    }
}
