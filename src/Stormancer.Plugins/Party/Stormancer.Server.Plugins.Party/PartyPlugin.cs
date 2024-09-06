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

using Stormancer.Abstractions.Server;
using Stormancer.Abstractions.Server.Components;
using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.PartyManagement;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameFinder;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Party.Interfaces;
using Stormancer.Server.Plugins.Party.JoinGame;
using Stormancer.Server.Plugins.Party.Model;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// A plugin for managing player parties.
    /// </summary>
    /// <remarks>
    /// A party is a group of players who want to play a game toghether.
    /// This group can queue as a single unit in a GameFinder.
    /// 
    /// Plugin settings:
    /// <c>party.clientAckTimeoutSeconds</c>: <c>decimal number > 0 ; default value = 2</c>
    ///     When a party's state changes, a notification is sent to every party member, who should respond with an acknowledgment.
    ///     This dictates how long the server should wait for this acknowledgment.
    ///     Note that the party's state is "frozen" while waiting for this ack ; operations on the party are queued until every member's ack is received, or the timeout is reached.
    /// </remarks>
    /// <seealso cref="GameFinder.GameFinderPlugin"/>
    class PartyPlugin : IHostPlugin
    {
        internal const string PARTYMANAGEMENT_METADATA_KEY = "stormancer.partymanagement";
        public const string PARTY_SCENE_TYPE = "party";
        public const string CLIENT_METADATA_KEY = "stormancer.party.plugin";
        public const string PARTY_MANAGEMENT_SCENEID = "party-manager";
        public const string PARTY_MANAGEMENT_SCENE_TYPE = "partyManager";

        //Service ids for service locator.
        public const string PARTY_MANAGEMENT_SERVICEID = "stormancer.plugins.partyManagement";
        public const string PARTY_SERVICEID = "stormancer.plugins.party";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<PartyAnalyticsWorker>().SingleInstance();
                builder.Register<CrossPlayPartyCompatibilityPolicy>().As<IPartyCompatibilityPolicy>();
                builder.Register<PartyService>(r => new PartyService(
                    r.Resolve<ISceneHost>(),
                    r.Resolve<ILogger>(),
                    r.Resolve<IUserSessions>(),
                    r.Resolve<GameFinderProxy>(),
                    r.Resolve<IServiceLocator>(),
                    r.Resolve<Func<IEnumerable<IPartyEventHandler>>>(),
                    r.Resolve<PartyState>(),
                    r.Resolve<RpcService>(),
                    r.Resolve<IConfiguration>(),
                    r.Resolve<IUserService>(),
                    r.Resolve<IEnumerable<IPartyPlatformSupport>>(),
                    r.Resolve<InvitationCodeService>(),
                    r.Resolve<PartyLuceneDocumentStore>(),
                    r.Resolve<PartyConfigurationService>(),
                    r.Resolve<IProfileService>(),
                    r.Resolve<PartyAnalyticsWorker>(),
                    r.Resolve<ISerializer>(),
                    r.Resolve<CrossplayService>(),
                    r.Resolve<IClusterSerializer>())
                ).As<IPartyService>().InstancePerRequest();

                builder.Register<PartyController>(r => new PartyController(
                    r.Resolve<ILogger>(),
                    r.Resolve<IUserSessions>(),
                    r.Resolve<IPartyService>(),
                    r.Resolve<ISerializer>())
                ).InstancePerRequest();

                builder.Register<PartyManagementService>(r => new PartyManagementService(
                    r.Resolve<InvitationCodeService>(),
                    r.Resolve<IScenesManager>(),
                    r.Resolve<IEnvironment>(),
                    r.Resolve<ISceneHost>(),
                    r.Resolve<IServiceLocator>())
                ).As<IPartyManagementService>().InstancePerRequest();

                builder.Register<PartyManagementController>(r => new PartyManagementController(
                    r.Resolve<IPartyManagementService>(),
                    r.Resolve<IUserSessions>(),
                    r.Resolve<ILogger>(),
                    r.Resolve<IEnumerable<IPartyEventHandler>>(),
                    r.Resolve<PartyConfigurationService>(),
                    r.Resolve<PartySearchService>()
                    )
                ).InstancePerRequest();

                builder.Register<PartyState>(r => new PartyState()).InstancePerScene();

               

                builder.Register<PartySceneLocator>(r => new PartySceneLocator()).As<IServiceLocatorProvider>();

                builder.Register<InvitationCodeService>(r => new InvitationCodeService(
                    r.Resolve<IHost>(),
                    r.Resolve<IClusterSerializer>(),
                    r.Resolve<IScenesManager>(),
                    r.Resolve<PartyConfigurationService>())
                ).AsSelf().SingleInstance();

                builder.Register<JoinGamePartyController>().InstancePerRequest();

                builder.Register<JoinGamesessionController>(r => new JoinGamesessionController(
                    r.Resolve<IGameSessionService>(),
                    r.Resolve<IUserSessions>(),
                    r.Resolve<IGameSessions>())
                ).InstancePerRequest();

                builder.Register<JoinGameSessionEventHandler>(r => new JoinGameSessionEventHandler(
                    r.Resolve<PartyProxy>(),
                    r.Resolve<IUserSessions>(),
                    r.Resolve<JoinGameSessionState>(),
                    r.Resolve<IConfiguration>(),
                    r.Resolve<ILogger>())
                ).As<IGameSessionEventHandler>().InstancePerRequest();

                builder.Register<JoinGameSessionState>(r => new JoinGameSessionState()
                ).InstancePerScene();

                builder.Register<PartyConfigurationService>(r => new PartyConfigurationService(r.Resolve<IConfiguration>())
                ).SingleInstance();

                builder.Register<PartyLuceneDocumentStore>(r => new PartyLuceneDocumentStore(
                    r.Resolve<ILucene>())
                ).As<ILuceneDocumentStore>().AsSelf().SingleInstance();

                builder.Register<PartySearchService>(r => new PartySearchService(
                    r.Resolve<SearchEngine>())
                );
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(PARTY_SCENE_TYPE, (ISceneHost scene) =>
                {
                    scene.AddParty();
                });
                host.AddSceneTemplate(PARTY_MANAGEMENT_SCENE_TYPE, (ISceneHost scene) =>
                {

                    scene.AddPartyManagement();
                });
                host.DependencyResolver.Resolve<PartyLuceneDocumentStore>().Initialize();
                host.DependencyResolver.Resolve<InvitationCodeService>().Initialize();
            };
            ctx.SceneShuttingDown += (ISceneHost scene) =>
              {
                  var state = scene.DependencyResolver.Resolve<PartyState>();
                  if (state.Settings != null)
                  {
                      scene.DependencyResolver.Resolve<PartyLuceneDocumentStore>().DeleteDocument(state.Settings.PartyId);
                  }
                  scene.DependencyResolver.Resolve<InvitationCodeService>().CancelCode(scene);

                  scene.DependencyResolver.Resolve<PartyAnalyticsWorker>().RemoveParty(scene.DependencyResolver.Resolve<PartyState>());

              };
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(PartyConstants.METADATA_KEY))
                {
                    scene.AddController<PartyController>();
                    scene.AddController<JoinGamePartyController>();
                    scene.DestroyWhenLastPlayerLeft();

                    scene.Starting.Add(async metadata =>
                    {
                        await using (var scope = scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                        {
                            var service = scope.Resolve<IPartyService>();
                            service.SetConfiguration(metadata);
                        }


                    });


                    scene.DependencyResolver.Resolve<PartyAnalyticsWorker>().AddParty(scene.DependencyResolver.Resolve<PartyState>());
                }

                if (scene.TemplateMetadata.ContainsKey("stormancer.gamesession"))
                {
                    scene.AddController<JoinGamesessionController>();
                }

                if (scene.TemplateMetadata.ContainsKey(PARTYMANAGEMENT_METADATA_KEY))
                {
                    scene.AddController<PartyManagementController>();

                }
            };
            ctx.SceneStarted += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(PartyConstants.METADATA_KEY))
                {
                    //scene.RunTask(ct => PartyService.RunReservationExpirationLoopAsync(scene, ct));
                }
            };
            ctx.HostStarted += (IHost host) =>
            {

                //Ensure PartyManagement scene exists.
                host.EnsureSceneExists(PARTY_MANAGEMENT_SCENEID, PARTY_MANAGEMENT_SCENE_TYPE, false, true);

                _ = host.DependencyResolver.Resolve<PartyAnalyticsWorker>().Run(CancellationToken.None);

            };

        }

    }
}
