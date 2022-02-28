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
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    /// <summary>
    /// Provides method to interact with server pools.
    /// </summary>
    public interface IServerPools
    {
        /// <summary>
        /// Tries to get a pool by id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pool"></param>
        /// <returns></returns>
        bool TryGetPool(string id, [NotNullWhen(true)] out IServerPool? pool);

       
    }

    internal class ServerPools : IServerPools, IConfigurationChangedEventHandler
    {
        private readonly ILogger logger;
        private readonly IConfiguration configuration;
        private readonly IEnumerable<IServerPoolProvider> providers;
        private readonly Dictionary<string, IServerPool> _pools = new Dictionary<string, IServerPool>();
        private object _poolsSyncRoot = new object();

        private record GameServerConnectionInfo(string sessionId, string poolId);
        private object _gameServersSyncRoot = new object();
        private Dictionary<string, GameServerConnectionInfo> _gameServers = new Dictionary<string, GameServerConnectionInfo>();

        public ServerPools(ILogger logger, IConfiguration config, IEnumerable<IServerPoolProvider> providers)
        {
            this.logger = logger;
            this.configuration = config;
            this.providers = providers;
            ApplySettings();
        }

        private bool TryCreateFromConfig(string poolId, JObject config, [NotNullWhen(true)] out IServerPool? pool)
        {

            foreach (var provider in providers)
            {
                if (provider.TryCreate(poolId, config, out pool))
                {
                    return true;
                }

            }
            pool = default;
            return false;
        }


        private void ApplySettings()
        {
            var config = configuration.Settings;
            var configs = ((JObject?)config.serverPools)?.ToObject<Dictionary<string,JObject>>();
            var destroyedPools = new List<string>();

            if(configs == null)
            {
                return;
            }

            lock (_poolsSyncRoot)
            {
                foreach (var (id, poolConfig) in configs)
                {
                    if (!_pools.ContainsKey(id) && TryCreateFromConfig(id, poolConfig, out var pool))
                    {
                        _pools.Add(id, pool);
                    }
                }

                foreach (var (poolId, pool) in _pools)
                {

                    if (configs.TryGetValue(poolId, out var c))
                    {
                        pool.UpdateConfiguration(c);
                    }
                    else
                    {
                        pool?.Dispose();
                        destroyedPools.Add(poolId);

                    }
                }
                foreach (var id in destroyedPools)
                {
                    _pools.Remove(id, out _);
                }

                
            }
        }


        internal async Task<GameServerStartupParameters?> WaitGameAvailableAsync(Session session, IScenePeerClient peer, CancellationToken cancellationToken)
        {
            IServerPool? selectedPool = null;
            lock (_poolsSyncRoot)
            {
                foreach (var (poolId, pool) in _pools)
                {
                    if(pool.CanManage(session, peer))
                    {
                        selectedPool = pool;
                        _gameServers[session.SessionId] = new GameServerConnectionInfo(session.SessionId, poolId);
                    }
                }
            }

            if(selectedPool!=null)
            {
               return await selectedPool.WaitGameSessionAsync(session, peer,cancellationToken);
            }
            else
            {
                return null;
            }

        }

        internal void RemoveGameServer(string sessionId)
        {
            lock(_poolsSyncRoot)
            {
                if(_gameServers.Remove(sessionId,out var infos))
                {
                    if(TryGetPool(infos.poolId,out var pool))
                    {
                        pool.OnGameServerDisconnected(sessionId);
                    }
                }
            }
           
        }

        private JObject? GetConfiguration(string id)
        {
            if (((Dictionary<string, JObject>)configuration.Settings.serverPools).TryGetValue(id, out var c))
            {
                return c;
            }
            else
            {
                return null;
            }
        }

        public bool TryGetPool(string id, [NotNullWhen(true)] out IServerPool? pool)
        {
            lock (_poolsSyncRoot)
            {
                return _pools.TryGetValue(id, out pool);
            }
        }

        public void OnConfigurationChanged()
        {
            ApplySettings();
        }

       
    }
}
