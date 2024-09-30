// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Abstractions.Server.Components;
using Stormancer.Cluster;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.DataProtection;
using Stormancer.Server.Plugins.GameSession.Admin;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    class GameSessionPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.gamesession";
        public const string P2PMESH_METADATA_KEY = "stormancer.p2pmesh";

        public const string POOL_SCENEID = "gamesession-serverpool";

        public const int SERVER_KEEPALIVE_SECONDS = 60 * 60;
        public const int SERVER_KEEPALIVE_INTERVAL_SECONDS = 25 * 60;

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(static r => new GameSessionController(r.Resolve<IGameSessionService>(), r.Resolve<IUserSessions>())).InstancePerRequest();
                builder.Register(static r => new ServerPoolController(r.Resolve<ServerPools>(), r.Resolve<IUserSessions>(), r.Resolve<IGameSessions>(), r.Resolve<AgentBasedGameServerProvider>())).InstancePerRequest();
                builder.Register(static r=> new AgentServerController(r.Resolve<AgentBasedGameServerProvider>())).InstancePerRequest();
                builder.Register(static r=>new DedicatedServerAuthProvider(r.Resolve<IDataProtector>())).As<IAuthenticationProvider>();
                builder.Register(static r=>new GameServerAgentAuthenticationProvider(r.Resolve<GameServerAgentConfiguration>())).As<IAuthenticationProvider>();
                builder.Register(static r=>new GameServerAgentConfiguration(r.Resolve<IConfiguration>(),r.Resolve<ISecretsStore>(),r.Resolve<IHttpClientFactory>())).As<IConfigurationChangedEventHandler>().AsSelf().SingleInstance();
                builder.Register(static r=> new DevDedicatedServerAuthProvider()).As<IAuthenticationProvider>();
                builder.Register(static r=> new GameSessions(r.Resolve<Lazy<IScenesManager>>(), r.Resolve<Utilities.RecyclableMemoryStreamProvider>(), r.Resolve<IEnvironment>(), r.Resolve<Lazy<GameSessionProxy>>(), r.Resolve<Lazy<IUserSessions>>(), r.Resolve<ISerializer>(), r.Resolve<JsonSerializer>())).As<IGameSessions>();

                builder.Register(static r => GameSessionsServiceLocator.Instance).As<IServiceLocatorProvider>();

                builder.Register(static r=>new CompositeServerPoolProvider(r.Resolve<Func<IServerPools>>(),r.Resolve<ILogger>())).As<IServerPoolProvider>();
                builder.Register(static r=> new DevServerPoolProvider(r.Resolve<ILogger>(),r.Resolve<GameSessionEventsRepository>())).As<IServerPoolProvider>().SingleInstance();
                builder.Register(static r=> new ProviderBasedServerPoolProvider(r.ResolveAll<IGameServerProvider>(),r.Resolve<ILogger>(), r.Resolve<ISceneHost>(), r.Resolve<IDataProtector>(), r.Resolve<GameSessionEventsRepository>())).As<IServerPoolProvider>().InstancePerScene();
                builder.Register(static r=>new GameSessionAnalyticsWorker(r.Resolve<IAnalyticsService>(),r.Resolve<GameSessionsRepository>())).SingleInstance();

                builder.Register(static r=> new AdminWebApiConfig()).As<IAdminWebApiConfig>();

                builder.Register(static r=>new DockerAgentAdminController(r.Resolve<ISceneHost>()));
                builder.Register(static r=> new GameSessionsAdminController(r.Resolve<ISceneHost>()));

                builder.Register(static r=>new GameSessionsRepository()).SingleInstance();
                builder.Register(static r=>new GameSessionEventsRepository(r.Resolve<Database.IESClientFactory>(),r.Resolve<ILogger>())).SingleInstance();
                builder.Register(static r=>new GameSessionsMonitoringService(r.Resolve<GameSessionProxy>(), r.Resolve<ServerPoolProxy>(),r.Resolve<GameSessionEventsRepository>())).InstancePerRequest();

                builder.Register(static d => new GameSessionState(d.Resolve<ISceneHost>()));

                builder.Register(d =>
                    new GameSessionService(
                        d.Resolve<GameSessionState>(),
                        d.Resolve<GameSessionAnalyticsWorker>(),
                        d.Resolve<ISceneHost>(),
                        d.Resolve<IConfiguration>(),
                        d.Resolve<IEnvironment>(),
                        d.Resolve<ILogger>(),
                        d.Resolve<RpcService>(),
                        d.Resolve<GameSessionsRepository>(),
                        d.Resolve<ISerializer>(),
                        d.Resolve<GameSessionEventsRepository>(),
                        d.Resolve<JsonSerializer>())
                )
                .As<IGameSessionService>()
                .As<IConfigurationChangedEventHandler>()
                .InstancePerScene();

            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(POOL_SCENEID, s =>
                {
                    s.TemplateMetadata["stormancer.serverPool"] = "1.0.0";
                    s.AddController<ServerPoolController>();
                    s.AddController<AgentServerController>();
                });

            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(POOL_SCENEID, POOL_SCENEID, false, true);

                _ = host.DependencyResolver.Resolve<GameSessionAnalyticsWorker>().Run(CancellationToken.None);
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<GameSessionController>();

                    scene.Starting.Add(metadata =>
                    {
                        var service = scene.DependencyResolver.Resolve<IGameSessionService>();

                        ((GameSessionService)service).SetConfiguration(metadata);
                        service.TryStart();
                        return Task.FromResult(true);

                    });


                }
                if (scene.TemplateMetadata.ContainsKey(P2PMESH_METADATA_KEY))
                {
                    scene.AddRoute("p2pmesh.relay", (message, origin) =>
                    {
                        if (SessionId.TryRead(message, out var sessionId, out var length) && scene.TryGetPeer(sessionId, out var peer))
                        {
                            var reliability = (PacketReliability)(message.Slice(length, 1).FirstSpan[0]);

                            var reader = new MessagePack.MessagePackReader(message.Slice(length + 1));

                            var route = reader.ReadString();
                            if (route != null)
                            {
                                scene.Send(peer.MatchPeerFilter, route, (writer, ctx) =>
                                {
                                    var (data, origin) = ctx;

                                    var span = writer.GetSpan((int)data.Length + origin.SessionId.Length);
                                    origin.SessionId.TryWriteBytes(span.Slice(0, origin.SessionId.Length));
                                    data.CopyTo(span.Slice(origin.SessionId.Length));
                                    writer.Advance((int)data.Length + origin.SessionId.Length);
                                }, PacketPriority.IMMEDIATE_PRIORITY, reliability, (message.Slice(length + 1 + reader.Consumed), origin));
                            }


                        }
                    });
                }
            };
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Id == POOL_SCENEID)
                {
                    builder.Register<AgentBasedGameServerProvider>().As<IGameServerProvider>().AsSelf().InstancePerScene();
                    builder.Register<ServerPools>().As<IServerPools>().AsSelf().As<IConfigurationChangedEventHandler>().InstancePerScene();
                }
            };
        }
    }

    class GameSessionsServiceLocator : IServiceLocatorProvider
    {
        public static GameSessionsServiceLocator Instance { get; } = new GameSessionsServiceLocator();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.plugins.gamesession")
            {
                ctx.SceneId = ctx.ServiceName;
            }
            if (ctx.ServiceType == "stormancer.plugins.serverPool" || ctx.ServiceType == "gameservers.agent")
            {
                ctx.SceneId = GameSessionPlugin.POOL_SCENEID;
            }
            return Task.CompletedTask;
        }
    }
}
