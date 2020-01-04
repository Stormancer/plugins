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
using Stormancer.Plugins;
using Stormancer.Server.PartyManagement;
using Stormancer.Server.Party.Model;
using System.Threading.Tasks;

namespace Stormancer.Server.Party
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
        internal const string PARTY_METADATA_KEY = "stormancer.party";
        internal const string PARTYMANAGEMENT_METADATA_KEY = "stormancer.partymanagement";
        public const string PARTY_SCENE_TYPE = "party";
        public const string CLIENT_METADATA_KEY = "stormancer.party.plugin";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<PartyService>().As<IPartyService>().InstancePerRequest();
                builder.Register<PartyController>().InstancePerRequest();
                builder.Register<PartyManagementService>().As<IPartyManagementService>().InstancePerRequest();
                builder.Register<PartyManagementController>().InstancePerRequest();
                builder.Register<PartyState>().InstancePerScene();
            };
            ctx.HostStarting += (host) => {
                host.AddSceneTemplate(PARTY_SCENE_TYPE, (ISceneHost scene) => {
                    scene.AddParty();
                });
            };
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(PARTY_METADATA_KEY))
                {
                    scene.AddController<PartyController>();


                    scene.Starting.Add(metadata =>
                    {
                        using (var scope = scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                        {
                            var service = scope.Resolve<IPartyService>();
                            service.SetConfiguration(metadata);
                        }

                        return Task.FromResult(true);
                    });
                }

                if (scene.Metadata.ContainsKey(PARTYMANAGEMENT_METADATA_KEY))
                {
                    scene.AddController<PartyManagementController>();
                }
            };

            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<PartyProxy>().As<IPartyProxy>();
            };
        }
    }
}
