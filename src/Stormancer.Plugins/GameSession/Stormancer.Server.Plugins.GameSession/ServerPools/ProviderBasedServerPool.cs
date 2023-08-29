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
using Stormancer.Server.Plugins.DataProtection;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    class ProviderBasedServerPoolProvider : IServerPoolProvider
    {
        private readonly IEnumerable<IGameServerProvider> serverProviders;
        private readonly ILogger logger;
        private readonly ISceneHost scene;
        private readonly IDataProtector _dataProtector;
        private readonly IGameSessions gameSessions;

        public ProviderBasedServerPoolProvider(IEnumerable<IGameServerProvider> serverProviders, ILogger logger, ISceneHost scene, IDataProtector dataProtector)
        {
            this.serverProviders = serverProviders;
            this.logger = logger;
            this.scene = scene;
            _dataProtector = dataProtector;
        }
        public bool TryCreate(string id, JObject config, [NotNullWhen(true)] out IServerPool? pool)
        {
            pool = null;
            var d = (dynamic)config;
            if ((string?)d?.type != "fromProvider")
            {
                return false;
            }

            var pId = (string?)d?.provider;
            if (pId == null)
            {
                return false;
            }
            var provider = serverProviders.FirstOrDefault(p => p.Type == pId);

            if (provider == null)
            {
                return false;
            }
            logger.Log(LogLevel.Info, "serverpools", $"Creating provider based pool {id} of type {pId}", new { });
            pool = new ProviderBasedServerPool(id, provider, logger, scene, _dataProtector);
            return true;

        }
    }
    class GameServerAuthClaims
    {
        public string ProviderType { get; set; }
        public string GameServerId { get; set; }
    }
    class ProviderBasedServerPool : IServerPool
    {
        private class GameServerRequest
        {
            public TaskCompletionSource<WaitGameServerResult> RequestCompletedCompletionSource { get; set; }
            public GameSessionConfiguration GameSessionConfiguration { get; set; }
            public string Id { get; set; }
            public CancellationToken CancellationToken { get; internal set; }
        }

        private readonly IGameServerProvider provider;
        private readonly ILogger logger;
        private readonly ISceneHost _scene;
        private readonly IDataProtector _dataProtector;
        private JObject config;
        private ConcurrentDictionary<string, Server> _startingServers = new ConcurrentDictionary<string, Server>();
        private ConcurrentDictionary<string, Server> _readyServers = new ConcurrentDictionary<string, Server>();
        private ConcurrentDictionary<string, Server> _runningServers = new ConcurrentDictionary<string, Server>();
        private ConcurrentQueue<GameServerRequest> _pendingRequests = new ConcurrentQueue<GameServerRequest>();

        private bool isRunning = true;
        public string Id { get; }
        public ProviderBasedServerPool(string id, IGameServerProvider provider, ILogger logger, ISceneHost scene, IDataProtector dataProtector)
        {
            Id = id;
            this.provider = provider;
            this.logger = logger;
            _scene = scene;
            _dataProtector = dataProtector;
            //Task.Run(async () =>
            //{
            //    isRunning = true;
            //    while (isRunning)
            //    {
            //        try
            //        {
            //            // resolve pending requests
            //            while (_pendingRequests.Any() && _readyServers.Any())
            //            {
            //                if (_pendingRequests.TryDequeue(out var request))
            //                {
            //                    var serverId = _readyServers.Keys.FirstOrDefault();

            //                    if (serverId != null && _readyServers.TryRemove(serverId, out var server))
            //                    {


            //                        request.RequestCompletedCompletionSource.SetResult(new GameServer { GameServerSessionId = server.Peer.SessionId });

            //                        _runningServers.TryAdd(serverId, server);

            //                        await using var scope = scene.CreateRequestScope();
            //                        var startupParameters = new GameServerStartupParameters
            //                        {
            //                            GameSessionConnectionToken = await scope.Resolve<IGameSessions>().CreateServerConnectionToken(request.Id, server.Id),
            //                            Config = request.GameSessionConfiguration,
            //                            GameSessionId = request.Id
            //                        };
            //                        server.RunTcs.TrySetResult(startupParameters);
            //                    }
            //                    else
            //                    {
            //                        _pendingRequests.Enqueue(request);
            //                    }
            //                }
            //            }

            //            //meet running servers requirements to satisfy requests + min ready server requirements
            //            var serversToStart = PendingServerRequests - _readyServers.Count - _startingServers.Count + MinServerReady;

            //            for (int i = 0; i < serversToStart; i++)
            //            {
            //                var guid = Guid.NewGuid().ToString();
            //                await StartServer(provider.Type + "/" + guid);
            //            }



            //            //clean timedout servers
            //            foreach (var server in _startingServers.Values.ToArray())
            //            {
            //                if (server.CreatedOn < DateTime.UtcNow - TimeSpan.FromSeconds(GameServerTimeout))
            //                {
            //                    if(_startingServers.TryRemove(server.Id, out _))
            //                    {
            //                        _ = provider.StopServer(server.Id,server.Context);
            //                    }
            //                }
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            logger.Log(LogLevel.Fatal, "serverpools." + provider.Type, "An error occured while running the server pool logic", ex);
            //        }
            //        await Task.Delay(1000);
            //    }

            //    foreach (var rq in _pendingRequests)
            //    {
            //        rq.RequestCompletedCompletionSource.TrySetCanceled();
            //    }

            //    while (_startingServers.Any() || _readyServers.Any())
            //    {
            //        foreach (var server in _readyServers.Values)
            //        {
            //            await provider.StopServer(server.Id,server.Context);
            //        }
            //        await Task.Delay(1000);
            //    }
            //});
        }




        public int ServersReady => _readyServers.Count;

        public int ServersStarting => _startingServers.Count;

        public int TotalServersInPool => ServersReady + ServersStarting + ServersRunning;

        public int MinServerReady { get; private set; }
        public int MaxServersInPool { get; private set; }
        public int GameServerTimeout { get; private set; }

        public int ServersRunning => _runningServers.Count;

        public int PendingServerRequests => _pendingRequests.Count(rq => !rq.CancellationToken.IsCancellationRequested);

        public bool CanAcceptRequest => ServersRunning + PendingServerRequests < MaxServersInPool;

        public void Dispose()
        {
            isRunning = false;
        }

        public async Task<WaitGameServerResult> TryWaitGameServerAsync(string gameSessionId, GameSessionConfiguration gsConfig, GameServerRecord record, CancellationToken cancellationToken)
        {
            if (!isRunning)
            {
                throw new InvalidOperationException("Pool not running");
            }
            var tcs = new TaskCompletionSource<WaitGameServerResult>();

            var claims = new GameServerAuthClaims { GameServerId = gameSessionId, ProviderType = provider.Type };
            var authToken = await _dataProtector.ProtectBase64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)), "gameServer");

            record.Pool = this.Id;
            record.PoolType = provider.Type;
            var result = await provider.TryStartServer(gameSessionId, authToken, this.config, record, gsConfig.PreferredRegions, cancellationToken);
            if (result.Success)
            {
                var server = new Server { Context = result.Context, GameServer = result.Instance, Id = gameSessionId, Record = record, Region = result.Region };
                _startingServers.TryAdd(gameSessionId, server);
                server.Context = result.Context;
                server.GameServer = result.Instance;
                server.GameSessionConfiguration = gsConfig;
                server.RequestCompletedCompletionSource = tcs;
                server.GameServer.OnClosed += () =>
                {
                    _startingServers.TryRemove(gameSessionId, out _);
                    _readyServers.TryRemove(gameSessionId, out _);
                    _runningServers.TryRemove(gameSessionId, out _);
                };
                return await tcs.Task;
            }
            else
            {
                return new WaitGameServerResult { Success = false };
            }


        }

        public void UpdateConfiguration(JObject config)
        {
            this.config = config;
            dynamic d = config;
            MinServerReady = ((int?)d.ready) ?? 0;
            MaxServersInPool = ((int?)d.ready) ?? int.MaxValue;
            GameServerTimeout = ((int?)d.serverTimeout) ?? 60;
        }



        public bool CanManage(Session session, IScenePeerClient peer)
        {
            return session.platformId.Platform == DedicatedServerAuthProvider.PROVIDER_NAME + "/" + provider.Type;
        }

        public async Task<GameServerStartupParameters?> WaitGameSessionAsync(Session session, IScenePeerClient client, CancellationToken cancellationToken)
        {
            var id = session.platformId.PlatformUserId;

            if (_startingServers.TryRemove(id, out var server))
            {
                server.Peer = client;

                _runningServers.TryAdd(id, server);

                await using var scope = this._scene.CreateRequestScope();
                var startupParameters = new GameServerStartupParameters
                {
                    GameSessionConnectionToken = await scope.Resolve<IGameSessions>().CreateServerConnectionToken(server.Id, server.Id),
                    Config = server.GameSessionConfiguration,
                    GameSessionId = server.Id
                };
                server.RequestCompletedCompletionSource.SetResult(new WaitGameServerResult
                {
                    Success = true,
                    Value = new GameServer
                    {
                        GameServerId = new GameServerId { Id = server.Id, PoolId = this.Id },
                        GameServerSessionId = client.SessionId
                    }
                });
                return startupParameters;
            }
            else
            {
                return null;
            }
        }

        public async Task OnGameServerDisconnected(string serverId, GameServerRecord gameServerRecord)
        {

            if (_runningServers.TryRemove(serverId, out var server))
            {
                await provider.StopServer(server.Id, server.Context);
            }
        }

        public async Task CloseServer(string serverId)
        {


            if (_runningServers.TryGetValue(serverId, out var server))
            {
                await server.Peer.Send("ServerPool.Shutdown", _ => { }, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
                await provider.StopServer(server.Id, server.Context);
            }
        }
    }
}
