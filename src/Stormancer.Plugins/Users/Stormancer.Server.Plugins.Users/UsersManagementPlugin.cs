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
using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users.Analytics;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// User plugin constants.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// Authenticator scene template id.
        /// </summary>
        public const string SCENE_TEMPLATE = "authenticator";

        /// <summary>
        /// Gets the authenticator scene id.
        /// </summary>
        /// <returns></returns>
        public static string GetSceneId()
        {
            return SCENE_TEMPLATE;
        }
        /// <summary>
        /// Type <see cref="string"/> for the 'users' service.
        /// </summary>
        public const string SERVICE_TYPE = "stormancer.authenticator";


        /// <summary>
        /// Gets the provider type for Stormancer.
        /// </summary>
        public const string PROVIDER_TYPE_STORMANCER = "stormancer";
    }



    class UsersManagementPlugin : Stormancer.Plugins.IHostPlugin
    {
        public UsersManagementPlugin()
        {
        }

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += HostStarting;
            ctx.HostStarted += HostStarted;
            ctx.HostDependenciesRegistration += RegisterDependencies;
            ctx.SceneDependenciesRegistration += RegisterSceneDependencies;
            ctx.SceneCreated += SceneCreated;
            ctx.SceneStarted += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    // Push authenticated users count
                    scene.RunTask(async ct =>
                    {
                        await using var scope = scene.CreateRequestScope();
                        var analytics = scope.Resolve<SessionsAnalyticsWorker>();
                        await analytics.Run(ct);
                    });
                    // Start periodic users credentials renewal
                    scene.RunTask(ct => scene.DependencyResolver.Resolve<CredentialsRenewer>().PeriodicRenewal(ct));
                }
            };
        }

        private void SceneCreated(ISceneHost scene)
        {
            if (scene.Template != Constants.SCENE_TEMPLATE)
            {
                //var index = scene.DependencyResolver.Resolve<UserSessionCache>();
                //scene.Connected.Add(index.OnConnected, 1000);
                //scene.Disconnected.Add(args => index.OnDisconnected(args.Peer));
            }
        }

        private void RegisterSceneDependencies(IDependencyBuilder b, ISceneHost scene)
        {
            if (scene.Template == Constants.SCENE_TEMPLATE)
            {
                b.Register(dr => new UserSessions(
                    dr.Resolve<IUserService>(),
                    dr.Resolve<SessionsRepository>(),
                    dr.Resolve<RecyclableMemoryStreamProvider>(),
                    dr.Resolve<Func<IEnumerable<IUserSessionEventHandler>>>(),
                    dr.Resolve<IClusterSerializer>(),
                    dr.Resolve<ISerializer>(),
                    dr.Resolve<Database.IESClientFactory>(),
                    dr.Resolve<IEnvironment>(), scene,
                    dr.Resolve<IConfiguration>(),
                    dr.Resolve<ILogger>())
                ).As<IUserSessions>().InstancePerRequest();

             

                b.Register(r=>new SessionsRepository()
                ).AsSelf().SingleInstance();

                b.Register(r=>new DeviceIdentifierAuthenticationProvider(
                    r.Resolve<IUserService>(),
                    r.Resolve<ILogger>())
                ).As<IAuthenticationProvider>();
                
              
                
                b.Register<LoginPasswordAuthenticationProvider>().As<IAuthenticationProvider>();
                b.Register<AdminImpersonationAuthenticationProvider>().As<IAuthenticationProvider>();
                b.Register<EphemeralAuthenticationProvider>().As<IAuthenticationProvider>();
                b.Register<CredentialsRenewer>().AsSelf().As<IAuthenticationEventHandler>().As<IConfigurationChangedEventHandler>().SingleInstance();
            }
            else
            {
                b.Register(dr => new UserSessionImpl(dr.Resolve<UserSessionProxy>(), dr.Resolve<IClusterSerializer>(), dr.Resolve<ISerializer>(), dr.Resolve<ISceneHost>())).As<IUserSessions>().InstancePerRequest();
            }
        }

        private void HostStarted(IHost host)
        {
            host.EnsureSceneExists(Constants.GetSceneId(), Constants.SCENE_TEMPLATE, true, true);
            
        }

        private void RegisterDependencies(IDependencyBuilder b)
        {
            //Indices
            b.Register<SessionsAnalyticsWorker>().SingleInstance();
            b.Register<CrossplayService>().InstancePerRequest();
            b.Register<SceneAuthorizationController>();
            b.Register(dr=> new UserSessionController(
                dr.Resolve<IUserSessions>(),
                dr.Resolve<IUserService>(),
                dr.Resolve<ISerializer>(),
                dr.Resolve<ISceneHost>(),
                dr.Resolve<IEnvironment>(),
                dr.Resolve<ILogger>(),
                dr.Resolve<IConfiguration>()));
            b.Register(dr => new CrossplayController(dr.Resolve<IUserSessions>(),dr.Resolve<CrossplayService>()));
            b.Register(dr => new AuthenticationController(
                dr.Resolve<IAuthenticationService>(), 
                dr.Resolve<IUserSessions>(), 
                dr.Resolve<RpcService>(),
                dr.Resolve<ISceneHost>())
            ).InstancePerRequest();

            b.Register(dr=>new AuthenticationService(
                dr.Resolve < Func < IEnumerable < IAuthenticationEventHandler >>>(),
                dr.Resolve<IEnumerable<IAuthenticationProvider>>(),
                dr.Resolve<IConfiguration>(),
                dr.Resolve<IUserService>(),
                dr.Resolve<IUserSessions>(),
                dr.Resolve<ILogger>(),
                dr.Resolve<ISceneHost>()
                
            )).As<IAuthenticationService>().InstancePerRequest();

            b.Register<LocatorProvider>(r=>new LocatorProvider(r.Resolve<IConfiguration>())
            ).As<IServiceLocatorProvider>();

            b.Register(dr => new UserService(
                dr.Resolve<Func<IEnumerable<IUserIdentityIdProvider>>>(),
                dr.ResolveOptional<IUserStorage>(),
                dr.Resolve<IEnvironment>(),
                dr.Resolve<ILogger>(),
                dr.Resolve<Func<IEnumerable<IUserEventHandler>>>(),
                dr.Resolve<IConfiguration>(),
                dr.Resolve<IAnalyticsService>())
            ).As<IUserService>().InstancePerRequest();


            b.Register<UsersAdminController>().InstancePerRequest();
            b.Register<AdminWebApiConfig>().As<IAdminWebApiConfig>();

            //b.Register<UserSessionCache>(dr => new UserSessionCache(dr.Resolve<ISceneHost>(), dr.Resolve<ISerializer>(), dr.Resolve<ILogger>())).AsSelf().InstancePerScene();
            b.Register(dr=>new PlatformSpecificServices(
                dr.Resolve<IEnumerable<IPlatformSpecificServiceImpl>>())
            ).As<IPlatformSpecificServices>();

            b.Register(dr=>new Analytics.AnalyticsEventHandler(
                dr.Resolve<IAnalyticsService>())
            ).As<IUserSessionEventHandler>();
        }

        private void HostStarting(IHost host)
        {
            host.AddSceneTemplate(Constants.SCENE_TEMPLATE, AuthenticatorSceneFactory);
        }

        private void AuthenticatorSceneFactory(ISceneHost scene)
        {
            //scene.AddController<GroupController>();
            scene.AddController<AuthenticationController>();
            scene.AddController<SceneAuthorizationController>();
            scene.AddController<UserSessionController>();
            scene.AddController<CrossplayController>();
        }
    }

    internal class LocatorProvider : IServiceLocatorProvider
    {
        private readonly IConfiguration config;

        public LocatorProvider(IConfiguration config)
        {
            this.config = config;
        }
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == Constants.SERVICE_TYPE)
            {
                string authenticatorSceneId = config.Settings?.auth?.sceneId ?? Constants.GetSceneId();
                ctx.SceneId = authenticatorSceneId;
            }
            return Task.CompletedTask;

        }
    }
}
