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
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Interfaces;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Steam;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    class SteamPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.plugins.steam";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(static r=> new SteamController(r.Resolve<IUserService>(),r.Resolve<ISteamService>())).InstancePerRequest();
                builder.Register(static r=> new SteamPartyController(r.Resolve<IPartyService>(),r.Resolve<IUserService>(),r.Resolve<ISteamService>())).InstancePerRequest();
                builder.Register(static r=>new SteamProfilePartBuilder(r.Resolve<IUserService>(),r.Resolve<ISteamService>(),r.Resolve<ILogger>())).As<IProfilePartBuilder>();
                builder.Register(static r => new ConfigurationMonitor<SteamConfigurationSection>(r.Resolve<IConfiguration>())).SingleInstance();
                builder.Register(static r=>new SteamService(r.Resolve<ConfigurationMonitor<SteamConfigurationSection>>(),r.Resolve<ILogger>(),r.Resolve<IUserSessions>(),r.Resolve<SteamKeyStore>())).As<ISteamService>();
                builder.Register(static r=> new SteamKeyStore(r.Resolve<IConfiguration>(),r.Resolve<Secrets.ISecretsStore>(),r.Resolve<ILogger>())).As<IConfigurationChangedEventHandler>().AsSelf().SingleInstance();
                builder.Register(static r=> new SteamFriendsEventHandler(r.Resolve<IUserSessions>(),r.Resolve<IUserService>(),r.Resolve<ISteamService>(),r.Resolve<ISceneHost>())).As<IFriendsEventHandler>();
                builder.Register(static r=> new SteamPartyEventHandler(r.Resolve<ISteamService>(),r.Resolve<ILogger>(),r.Resolve<ISceneHost>())).As<IPartyEventHandler>().InstancePerRequest();
                builder.Register(static r=> SteamServiceLocator.Instance).As<IServiceLocatorProvider>();
                builder.Register(static r=> new SteamPlatformInvitationsHandler(r.Resolve<ISceneHost>(),r.Resolve<IFriendsService>())).As<IPartyPlatformSupport>().InstancePerRequest();
                builder.Register(static r=> new SteamAuthenticationProvider(r.Resolve<ConfigurationMonitor<SteamConfigurationSection>>(),r.Resolve<ILogger>(),r.Resolve<IUserService>(),r.Resolve<ISteamService>())).As<IAuthenticationProvider>();
            };

            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    scene.TemplateMetadata[METADATA_KEY] = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    scene.AddController<SteamController>();
                    
                }

                if (scene.TemplateMetadata.ContainsKey(PartyConstants.METADATA_KEY))
                {
                    scene.AddController<SteamPartyController>();
                }
            };
        }
    }


    internal class SteamServiceLocator : IServiceLocatorProvider
    {
        public static SteamServiceLocator Instance { get; } = new SteamServiceLocator();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.steam")
            {
                ctx.SceneId = Constants.GetSceneId();
            }
            return Task.CompletedTask;
        }
    }
}

