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
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    class CompositeServerPoolProvider : IServerPoolProvider
    {

        private readonly Func<IServerPools> pools;
        private readonly ILogger logger;

        public CompositeServerPoolProvider(Func<IServerPools> pools, ILogger logger)
        {

            this.pools = pools;
            this.logger = logger;
        }
        public bool TryCreate(string id, JObject config, [NotNullWhen(true)] out IServerPool? pool)
        {
            if (config["type"]?.ToObject<string>() == "composite")
            {
                pool = new CompositeServerPool(id, pools, logger);
                return true;
            }
            else
            {
                pool = default;
                return false;
            }

        }
    }

    class CompositeServerPool : IServerPool
    {
        private List<string> _subPools = new List<string>();

        public IEnumerable<IServerPool> SubPools => _subPools.Select(p => pools().TryGetPool(p, out var pool) ? pool : null).WhereNotNull();
        private JObject config;
        private readonly Func<IServerPools> pools;
        private readonly ILogger logger;

        public int ServersReady => SubPools.Sum(p => p.ServersReady);

        public int ServersStarting => SubPools.Sum(p => p.ServersStarting);

        public int TotalServersInPool => ServersReady + ServersStarting;

        public int MinServerReady { get; private set; }

        public string Id { get; }

        public int ServersRunning => SubPools.Sum(p => p.ServersRunning);

        public int MaxServersInPool => SubPools.Sum(p => p.MaxServersInPool);

        public int PendingServerRequests => SubPools.Sum(p => p.MaxServersInPool);

        public bool CanAcceptRequest => SubPools.Any(p => p.CanAcceptRequest);

        public CompositeServerPool(string id, Func<IServerPools> pools, ILogger logger)
        {
            Id = id;
            this.pools = pools;
            this.logger = logger;
        }
        public void Dispose()
        {

        }

        public async Task<WaitGameServerResult> TryWaitGameServerAsync(string gameSessionId, GameSessionConfiguration config, CancellationToken cancellationToken)
        {

            foreach (var subPool in _subPools)
            {
                if (pools().TryGetPool(subPool, out var pool))
                {
                    var result = await pool.TryWaitGameServerAsync(gameSessionId, config, cancellationToken);
                    if (result.Success)
                    {
                        return result;
                    }
                }
            }

            return new WaitGameServerResult { Success = false };

        }



        public void UpdateConfiguration(JObject config)
        {
            this.config = config;
            dynamic d = config;
            MinServerReady = ((int?)d.ready) ?? 0;
            var poolIds = ((JArray)(d.pools))?.ToObject<List<string>>() ?? Enumerable.Empty<string>();
            _subPools.Clear();
            foreach (var poolId in poolIds)
            {

                _subPools.Add(poolId);

            }
        }

        public bool CanManage(Session session, IScenePeerClient peer) => false;


        public Task<GameServerStartupParameters?> WaitGameSessionAsync(Session session, IScenePeerClient client, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task OnGameServerDisconnected(string serverId)
        {
            foreach (var pool in SubPools)
            {
                await pool.OnGameServerDisconnected(serverId);
            }
        }

        public async Task CloseServer(string serverId)
        {
            foreach (var pool in SubPools)
            {
                await pool.CloseServer(serverId);
            }
        }

        /// <summary>
        /// Queries the logs of a game servers. 
        /// </summary>
        /// <remarks>
        /// Not supported.
        /// </remarks>
        /// <param name="gameSessionId"></param>
        /// <param name="since"></param>
        /// <param name="until"></param>
        /// <param name="size"></param>
        /// <param name="follow"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<string> QueryLogsAsync(string gameSessionId, DateTime? since, DateTime? until, uint size, bool follow, CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Empty<string>();
        }

        public Task<bool> KeepServerAlive(string gameSessionId)
        {
            return Task.FromResult(false);
        }
    }
}
