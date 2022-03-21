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

using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    class GameSessionPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.gamesession";

        public const string POOL_SCENEID = "gamesession-serverpool";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<GameSessionController>().InstancePerRequest();
                builder.Register<ServerPoolController>().InstancePerRequest();
                builder.Register<DedicatedServerAuthProvider>().As<IAuthenticationProvider>();
                builder.Register<DevDedicatedServerAuthProvider>().As<IAuthenticationProvider>();
                builder.Register<GameSessions>().As<IGameSessions>();
                builder.Register<ServerPools>().As<IServerPools>().AsSelf().As<IConfigurationChangedEventHandler>().InstancePerScene();
                builder.Register<GameSessionsServiceLocator>().As<IServiceLocatorProvider>();
                builder.Register<DevServerPoolProvider>().As<IServerPoolProvider>().SingleInstance();
                builder.Register<ProviderBasedServerPoolProvider>().As<IServerPoolProvider>().InstancePerScene();
                builder.Register<DockerGameServerProvider>().As<IGameServerProvider>().SingleInstance();
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(POOL_SCENEID, s =>
                {
                    s.Metadata["stormancer.serverPool"] = "1.0.0";
                    s.AddController<ServerPoolController>();
                });
               
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(POOL_SCENEID, POOL_SCENEID, false, true);
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<GameSessionController>();

                    scene.Starting.Add(metadata =>
                    {
                        var service = scene.DependencyResolver.Resolve<IGameSessionService>();
                        service.SetConfiguration(metadata);
                        service.TryStart();
                        return Task.FromResult(true);

                    });
                }
            };
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    builder.Register(d => new GameSessionState(scene));
                    builder.Register(d =>
                        new GameSessionService(
                            d.Resolve<GameSessionState>(),
                            scene,
                            d.Resolve<IConfiguration>(),
                            d.Resolve<IEnvironment>(),
                            d.Resolve<Management.ManagementClientProvider>(),
                            d.Resolve<ILogger>(),
                            d.Resolve<IAnalyticsService>(),
                            d.Resolve<RpcService>(),
                            d.Resolve<ISerializer>())
                    )
                    .As<IGameSessionService>()
                    .As<IConfigurationChangedEventHandler>()
                    .InstancePerScene();

                }
            };
        }
    }

    class GameSessionsServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.plugins.gamesession")
            {
                ctx.SceneId = ctx.ServiceName;
            }
            if (ctx.ServiceType == "stormancer.plugins.serverPool")
            {
                ctx.SceneId = GameSessionPlugin.POOL_SCENEID;
            }
            return Task.CompletedTask;
        }
    }
}
