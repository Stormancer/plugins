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

using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
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

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<GameSessionController>().InstancePerRequest();
                builder.Register<DedicatedServerAuthProvider>().As<IAuthenticationProvider>();
                builder.Register<GameSessions>().As<IGameSessions>();
                builder.Register<ServerPools>().As<IServerPools>().AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();
                builder.Register<GameSessionsServiceLocator>().As<IServiceLocatorProvider>();
            };

            ctx.HostStarted += (IHost host) =>
            {
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
                    builder.Register(d =>
                        new GameSessionService(
                            d.Resolve<IServerPools>(),
                            scene,
                            d.Resolve<IConfiguration>(),
                            d.Resolve<IEnvironment>(),
                            d.Resolve<IDelegatedTransports>(),
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
            if(ctx.ServiceType == "stormancer.plugins.gamesession")
            {
                ctx.SceneId = ctx.ServiceName;
            }
            return Task.CompletedTask;
        }
    }

    class DedicatedServerAuthProvider : IAuthenticationProvider
    {
        public const string PROVIDER_NAME = "dedicatedServer";
        private IEnvironment _env;

        public string Type => PROVIDER_NAME;
        public DedicatedServerAuthProvider(IEnvironment env)
        {
            _env = env;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            //result.Add("provider.dedicatedServer", "enabled");
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var token = authenticationCtx.Parameters["token"];
            var appInfos = await _env.GetApplicationInfos();

            try
            {
                //TODO: Reimplement security.
                var gameId = token;

                return AuthenticationResult.CreateSuccess(new User { Id = "ds-" + gameId }, new PlatformId { PlatformUserId = gameId, Platform = PROVIDER_NAME }, authenticationCtx.Parameters);
            }
            catch (Exception ex)
            {
                return AuthenticationResult.CreateFailure("Invalid token :" + ex.ToString(), new PlatformId { Platform = PROVIDER_NAME }, authenticationCtx.Parameters);
            }
        }

        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new NotSupportedException();
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task Unlink(User user)
        {
            throw new NotSupportedException();
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }
    }
}
