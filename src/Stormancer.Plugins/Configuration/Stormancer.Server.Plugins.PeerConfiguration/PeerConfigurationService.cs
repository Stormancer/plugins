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
using Autofac;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PeerConfiguration
{
    /// <summary>
    /// Classes implementing this contract can customize configuration sent to peers.
    /// </summary>
    public interface IPeerConfigurationEventHandler
    {
        /// <summary>
        /// Generates the configuration sent to a peer from the section in the server app config.
        /// </summary>
        /// <remarks>
        /// If several classes implement <see cref="IPeerConfigurationEventHandler"/>, they are called iteratively to modify the config.
        /// <see cref="PriorityAttribute"/> can be used to order execution
        /// </remarks>
        /// <param name="group"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        ValueTask<JObject> FilterPeerConfiguration(string group, JObject configuration) => ValueTask.FromResult(configuration);

        /// <summary>
        /// Gets the configuration group associated with a peer.
        /// </summary>
        /// <remarks>
        /// Configuration groups enable to group peer by identical configuration (for instance groups "client" and "server").
        /// Use <see cref="FilterPeerConfiguration(string, JObject)"/> to customize the configuration sent to each group.
        /// </remarks>
        /// <param name="scenePeerClient"></param>
        /// <returns></returns>
        ValueTask<string?> GetGroup(IScenePeerClient scenePeerClient);
    }

    class PeerConfigurationService : IConfigurationChangedEventHandler
    {
        private readonly IConfiguration configuration;
        private readonly ISceneHost scene;
        private readonly ISerializer serializer;

        public PeerConfigurationService(IConfiguration configuration, ISceneHost scene, ISerializer serializer)
        {
            this.configuration = configuration;
            this.scene = scene;
            this.serializer = serializer;
        }

        void IConfigurationChangedEventHandler.OnConfigurationChanged()
        {
            _ = BroadcastConfiguration();
        }

        private async ValueTask BroadcastConfiguration()
        {
            await using var scope = scene.CreateRequestScope();

            var handlers = GetEventHandlers(scope);

            var peers = scene.RemotePeers
                .Where(p => p.Metadata.ContainsKey(PeerConfigurationPlugin.METADATA_KEY))
                .Select(p => (GetGroup(p, handlers), p)).ToList();

            foreach(var (t,p) in peers)
            {
                await t;
            }

            var groups = peers.GroupBy(tuple => tuple.Item1.Result, tuple => tuple.p);
            foreach(var group in groups)
            {
                var config = await GetConfigurationForGroup(group.Key, handlers);

                await scene.Send(new MatchArrayFilter(group), "peerConfig.update", s => serializer.Serialize(config.ToString(), s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
            }
            
        }

        private ValueTask<string> GetGroup(IScenePeerClient peer, IEnumerable<IPeerConfigurationEventHandler> handlers)
        {
            if (peer.Metadata.TryGetValue(PeerConfigurationPlugin.METADATA_GROUP_KEY, out var group))
            {
                return ValueTask.FromResult(group);
            }
            else
            {
                return GetGroupAsync(peer, handlers);
            }

            static async ValueTask<string> GetGroupAsync(IScenePeerClient peer, IEnumerable<IPeerConfigurationEventHandler> handlers)
            {
                foreach (var handler in handlers)
                {
                    var group = await handler.GetGroup(peer);
                    if (group != null)
                    {
                        return group;
                    }
                }
                return "default";
            }
        }

        private async ValueTask<JObject> GetConfigurationForGroup(string group, IEnumerable<IPeerConfigurationEventHandler> handlers)
        {
            var config = configuration.GetValue("peerConfig", new JObject());


            foreach (var handler in handlers)
            {
                config = await handler.FilterPeerConfiguration(group, config);
            }

            return config;
        }

        public async ValueTask SendConfiguration(IScenePeerClient client)
        {
            await using var scope = scene.CreateRequestScope();

            var handlers = GetEventHandlers(scope);

            var group = await GetGroup(client,handlers);

            var config = await GetConfigurationForGroup(group,handlers);

            await scene.Send(new MatchPeerFilter(client), "peerConfig.update", s => serializer.Serialize(config.ToString(), s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);

        }

        private IEnumerable<IPeerConfigurationEventHandler> GetEventHandlers(IDependencyResolver scope)
        {
            return scope.Resolve<IEnumerable<IPeerConfigurationEventHandler>>().OrderByDescending(h => h.GetType().GetCustomAttributes(typeof(PriorityAttribute), false).Cast<PriorityAttribute>().FirstOrDefault()?.Priority ?? 0);
        }

    }
}
