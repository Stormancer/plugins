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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession.Models;
using Stormancer.Server.Plugins.Management;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stormancer.Plugins;
using System.Runtime.CompilerServices;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Models;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Server status
    /// </summary>
    public enum ServerStatus
    {
        /// <summary>
        /// Server waiting players.
        /// </summary>
        WaitingPlayers = 0,
        /// <summary>
        /// All players connected to server.
        /// </summary>
        AllPlayersConnected = 1,

        /// <summary>
        /// Server starting.
        /// </summary>
        Starting = 2,

        /// <summary>
        /// Server started.
        /// </summary>
        Started = 3,

        /// <summary>
        /// Server shut down.
        /// </summary>
        Shutdown = 4,

        /// <summary>
        /// Server faulted.
        /// </summary>
        Faulted = 5
    }

    /// <summary>
    /// Player status.
    /// </summary>
    public enum PlayerStatus
    {
        /// <summary>
        /// Player not connected.
        /// </summary>
        NotConnected = 0,

        /// <summary>
        /// Player connected.
        /// </summary>
        Connected = 1,

        /// <summary>
        /// Player ready.
        /// </summary>
        Ready = 2,

        /// <summary>
        /// Player in faulted state.
        /// </summary>
        Faulted = 3,

        /// <summary>
        /// Player disconnected.
        /// </summary>
        Disconnected = 4
    }

    internal class GameSessionService : IGameSessionService, IConfigurationChangedEventHandler, IAsyncDisposable
    {
        private class Client
        {
            public Client(IScenePeerClient? peer)
            {
                Peer = peer;
                Reset();
                Status = PlayerStatus.NotConnected;
            }

            public void Reset()
            {
                GameCompleteTcs?.TrySetCanceled();
                GameCompleteTcs = new TaskCompletionSource<Action<Stream, ISerializer>>();
                ResultData = null;
            }

            public IScenePeerClient? Peer { get; set; }

            public Stream? ResultData { get; set; }

            public PlayerStatus Status { get; set; }

            public string? FaultReason { get; set; }

            public TaskCompletionSource<Action<Stream, ISerializer>>? GameCompleteTcs { get; private set; }
        }

        // Constant variable
        private const string LOG_CATEOGRY = "gamesession";
        private const string P2P_TOKEN_ROUTE = "player.p2ptoken";
        private const string ALL_PLAYER_READY_ROUTE = "players.allReady";

        // Stormancer object

        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ISceneHost _scene;
        private readonly IEnvironment _environment;
        private readonly IDelegatedTransports _pools;
        private readonly RpcService _rpc;
        private readonly ISerializer _serializer;

        private TimeSpan _gameSessionTimeout = TimeSpan.MaxValue;
        private GameSessionConfiguration? _config;
        private readonly CancellationTokenSource _sceneCts = new();


        private readonly byte[]? _serverGuid;

        private readonly ConcurrentDictionary<string, Client> _clients = new();
        private ServerStatus _status = ServerStatus.WaitingPlayers;
        // A source that is canceled when the game session is complete
        private readonly CancellationTokenSource _gameCompleteCts = new();


        private string? _p2pToken;


        private readonly object _lock = new();
        private readonly IAnalyticsService _analytics;
        private readonly ManagementClientProvider _management;
        private TaskCompletionSource<IScenePeerClient>? _serverPeer = null;
        private ShutdownMode _shutdownMode;
        private DateTime _shutdownDate;

        public GameSessionService(
            ISceneHost scene,
            IConfiguration configuration,
            IEnvironment environment,
            IDelegatedTransports pools,
            ManagementClientProvider management,
            ILogger logger,
            IAnalyticsService analytics,
            RpcService rpc,
            ISerializer serializer)
        {
            _analytics = analytics;
            _management = management;
            _scene = scene;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
            _pools = pools;
            _rpc = rpc;
            _serializer = serializer;


            ApplySettings();

            scene.Shuttingdown.Add(args =>
            {
                _sceneCts.Cancel();
                return Task.CompletedTask;
            });
            scene.Connecting.Add(this.PeerConnecting);
            scene.ConnectionRejected.Add(this.PeerConnectionRejected);
            scene.Connected.Add(this.PeerConnected);
            scene.Disconnected.Add((args) => this.PeerDisconnecting(args.Peer));
            scene.AddRoute("player.ready", this.ReceivedReady, _ => _);
            scene.AddRoute("player.faulted", this.ReceivedFaulted, _ => _);
        }

        private void ApplySettings()
        {
            dynamic settings = _configuration.Settings;

            var timeout = ((string?)settings?.gameServer?.dedicatedServerTimeout);
            if (timeout != null)
            {
                _gameSessionTimeout = TimeSpan.Parse(timeout, CultureInfo.InvariantCulture);
            }
            else
            {
                _gameSessionTimeout = TimeSpan.MaxValue;
            }
        }

        private async Task<string?> GetUserId(IScenePeerClient peer)
        {
            var existingClient = _clients.FirstOrDefault(client => client.Value.Peer == peer);
            if (existingClient.Key != null)
            {
                return existingClient.Key;
            }
            else
            {
                await using var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag);
                var sessions = scope.Resolve<IUserSessions>();
                return (await sessions.GetUser(peer, CancellationToken.None))?.Id;
            }
        }

        private async Task ReceivedReady(Packet<IScenePeerClient> packet)
        {
            try
            {
                var peer = packet.Connection;
                if (peer == null)
                {
                    throw new ArgumentNullException(nameof(peer));
                }
                if (peer.ContentType == "application/server-id" && _serverGuid !=null)
                {
                    var peerGuid = new Guid(peer.UserData);
                    var serverGuid = new Guid(_serverGuid);
                    if (serverGuid == peerGuid)
                    {
                        await SignalServerReady(peer.SessionId);
                        return;
                    }
                }
                if (IsWorker(packet.Connection))
                {
                    if (_status == ServerStatus.Started)
                    {
                        await _scene.Send(new MatchPeerFilter(peer), P2P_TOKEN_ROUTE, s =>
                        {
                            var serializer = peer.Serializer();
                            serializer.Serialize(_p2pToken, s);
                        }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                    }
                    return;
                }

                var user = await GetUserId(peer);

                if (user == null)
                {
                    throw new InvalidOperationException("Unauthenticated peer.");
                }

                if (!_clients.TryGetValue(user, out var currentClient))
                {
                    throw new InvalidOperationException("Unknown client.");
                }

                _logger.Log(LogLevel.Trace, "gamesession", "received a ready message from an user.", new { userId = user, currentClient.Status });

                if (currentClient.Status < PlayerStatus.Ready)
                {
                    currentClient.Status = PlayerStatus.Ready;
                    var ctx = new ClientReadyContext(peer);

                    await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                    {
                        await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnClientReady(ctx), ex =>
                        {
                            _logger.Log(LogLevel.Error, "gameSession", "An error occured while running gameSession.OnClientReady event handlers", ex);
                        });
                    }
                    BroadcastClientUpdate(currentClient, user, packet.ReadObject<string>());
                }

                await CheckAllPlayersReady();

                if (IsHost(peer.SessionId) && (((bool?)_configuration.Settings.gameSession?.usep2p) == true))
                {
                    var p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(peer.SessionId, _scene.Id);

                    _p2pToken = p2pToken;

                    foreach (var p in _scene.RemotePeers.Where(p => p != peer))
                    {
                        p.Send(P2P_TOKEN_ROUTE, p2pToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "gamesession", "an error occurred while receiving a ready message", ex);
                throw;
            }
        }

        // pseudoBool to use with interlocked
        private int _readySent = 0;
        private async Task CheckAllPlayersReady()
        {
            Debug.Assert(_config != null);
            if (_config.UserIds.Count() == _clients.Count)
            {
                if (_clients.Values.All(c => c.Status == PlayerStatus.Ready) && System.Threading.Interlocked.CompareExchange(ref _readySent, 1, 0) == 0)
                {
                    _logger.Log(LogLevel.Trace, "gamesession", "Send all player ready", new { });
                    await _scene.Send(new MatchAllFilter(), ALL_PLAYER_READY_ROUTE, s => { }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                }
            }
        }

        private void BroadcastClientUpdate(Client client, string userId, string? data = null)
        {
            Debug.Assert(_config != null);

            _scene.Broadcast("player.update", new PlayerUpdate { UserId = userId, Status = (byte)client.Status, Data = data ?? "", IsHost = (_config.HostUserId == userId) }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
        }

        private async Task ReceivedFaulted(Packet<IScenePeerClient> packet)
        {
            var peer = packet.Connection;
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }
            var user = await GetUserId(peer);

            if (user == null)
            {
                throw new InvalidOperationException("Unauthenticated peer.");
            }

            if (!_clients.TryGetValue(user, out var currentClient))
            {
                throw new InvalidOperationException("Unknown client.");
            }

            _ = packet.ReadObject<string>(); // Discard reason string
            currentClient.Status = PlayerStatus.Faulted;

            if (this._status == ServerStatus.WaitingPlayers
                || this._status == ServerStatus.AllPlayersConnected)
            {
                this._status = ServerStatus.Faulted;
            }
        }

        public void SetConfiguration(dynamic metadata)
        {
            if (metadata.gameSession != null)
            {
                _config = ((JObject)metadata.gameSession).ToObject<GameSessionConfiguration>();
            }
        }

        private static bool IsWorker(IScenePeerClient peer)
        {
            return peer.ContentType == "application/server-id";
        }

        private async Task PeerConnecting(IScenePeerClient peer)
        {
            if (peer.ContentType == "application/server-id" && !IsServer(peer))
            {
                throw new ClientException("Failed to authenticate as dedicated server");
            }

            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }
            var user = await GetUserId(peer);

            if (user == null)
            {
                throw new ClientException("You are not authenticated.");
            }

            if (_config == null)
            {
                throw new InvalidOperationException("Game session plugin configuration missing in scene instance metadata. Please check the scene creation process.");
            }

            if (!_config.Public && !_config.UserIds.Contains(user))
            {
                throw new ClientException("You are not authorized to join this game.");
            }

            var client = new Client(peer);
            lock (_clients)
            {
                if (!_clients.TryAdd(user, client))
                {
                    if (_clients.TryGetValue(user, out var alreadyConnectedClient))
                    {
                        if (alreadyConnectedClient.Status != PlayerStatus.Disconnected)
                        {
                            throw new ClientException("User already connected to gamesession.");
                        }
                        else if (!_clients.TryUpdate(user, client, alreadyConnectedClient))
                        {
                            throw new ClientException("Failed to update peer associated with user.");
                        }
                    }
                }
            }
        }

        private async Task PeerConnectionRejected(IScenePeerClient peer)
        {
            lock (_clients)
            {
                var client = _clients.FirstOrDefault(kvp => kvp.Value.Peer == peer);
                _clients.TryRemove(client.Key, out _);
            }
            await Task.CompletedTask;
        }

        private async Task SignalServerReady(string sessionId)
        {
            _p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(sessionId, _scene.Id);

            foreach (var p in _scene.RemotePeers.Where(p => p.SessionId != sessionId))
            {
                p.Send(P2P_TOKEN_ROUTE, _p2pToken);
            }
            //_scene.Broadcast(P2P_TOKEN_ROUTE, _p2pToken);
            _status = ServerStatus.Started;
        }

        public bool IsServer(IScenePeerClient peer)
        {
            if(_serverGuid == null)
            {
                return false;
            }

            if (peer != null && peer.ContentType == "application/server-id")
            {
                var peerGuid = new Guid(peer.UserData);
                var serverGuid = new Guid(_serverGuid);
                return serverGuid == peerGuid;
            }
            else
            {
                return false;
            }
        }

        private async Task PeerConnected(IScenePeerClient peer)
        {
            Debug.Assert(_config != null);

            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            var client = _clients.First(client => client.Value.Peer == peer);
            client.Value.Status = PlayerStatus.Connected;
            if (!_config.Public)
            {
                BroadcastClientUpdate(client.Value, client.Key);
            }

            await TryStart();

            if (IsServer(peer))
            {
                GetServerTcs().TrySetResult(peer);

                peer.Send(P2P_TOKEN_ROUTE, "");
                return;
            }
            if (!IsWorker(peer))
            {
                var userId = client.Key;

                _analytics.Push("gamesession", "playerJoined", JObject.FromObject(new { userId, gameSessionId = this._scene.Id, sessionId = peer.SessionId }));
                //Check if the gameSession is Dedicated or listen-server            

                // If the host is not defined a P2P was sent with "" to notify client is host.
                _logger.Log(LogLevel.Trace, "gamesession", $"Gamesession {_scene.Id} evaluating {userId} as host (expected host :{_config.HostUserId})", new { });
                if (string.IsNullOrEmpty(_config.HostUserId) || _config.HostUserId == userId)
                {
                    _config.HostUserId = userId;
                    if (GetServerTcs().TrySetResult(peer))
                    {
                        _logger.Log(LogLevel.Debug, LOG_CATEOGRY, "Host defined and connecting", userId);
                        peer.Send(P2P_TOKEN_ROUTE, "");
                    }
                    else
                    {
                        _logger.Log(LogLevel.Debug, LOG_CATEOGRY, "Client connecting", userId);
                    }
                }


                foreach (var uId in _clients.Keys)
                {
                    if (uId != userId)
                    {
                        var currentClient = _clients[uId];
                        var isHost = GetServerTcs().Task.IsCompleted && GetServerTcs().Task.Result.SessionId == currentClient.Peer?.SessionId;
                        peer.Send("player.update",
                            new PlayerUpdate { UserId = uId, IsHost = isHost, Status = (byte)currentClient.Status, Data = currentClient.FaultReason ?? "" },
                            PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                    }
                }
                if (_status == ServerStatus.Started)
                {
                    if (_p2pToken == null && GetServerTcs().Task.IsCompleted)
                    {
                        _p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken((await GetServerTcs().Task).SessionId, _scene.Id);
                    }

                    peer.Send(P2P_TOKEN_ROUTE, _p2pToken);
                }

                var playerConnectedCtx = new ClientConnectedContext(this, new PlayerPeer(peer, new Player(peer.SessionId, userId)), _config.HostUserId == userId);
                await using var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag);
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                    h => h.OnClientConnected(playerConnectedCtx),
                    ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing OnClientConnected event", ex));
            }
        }

        private Task? _serverStartTask = null;
        public Task TryStart()
        {
            lock (this._lock)
            {
                if (_serverStartTask == null)
                {
                    _serverStartTask = Start();
                }
            }
            return _serverStartTask;
        }

        private async Task Start()
        {
            Debug.Assert(_config != null);

            var ctx = new GameSessionContext(this._scene, _config, this);
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(h => h.GameSessionStarting(ctx), ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing GameSessionStarting event", ex));
            }


            _logger.Log(LogLevel.Trace, "gamesession", "No server executable enabled. Game session started.", new { });
            _status = ServerStatus.Started;
            return;
        }

        private TaskCompletionSource<IScenePeerClient> GetServerTcs()
        {
            lock (_lock)
            {
                if (_serverPeer == null)
                {
                    _serverPeer = new TaskCompletionSource<IScenePeerClient>();
                }
            }
            return _serverPeer;
        }

        public Task PeerDisconnecting(IScenePeerClient peer)
        {
            Debug.Assert(_config != null);
            if (IsHost(peer.SessionId))
            {
                lock (_lock)
                {
                    _serverPeer = null;
                }
            }
            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }
            _analytics.Push("gamesession", "playerLeft", JObject.FromObject(new { sessionId = peer.SessionId, gameSessionId = this._scene.Id }));
            Client? client = null;
            string? userId = null;
            lock (_clients)
            {
                // the peer disconnected from the app and is not in the sessions anymore.
                foreach (var kvp in _clients)
                {
                    if (kvp.Value.Peer == peer)
                    {
                        userId = kvp.Key;
                        client = kvp.Value;

                        if (_config.Public)
                        {
                            _clients.TryRemove(userId, out _);
                        }
                        // no need to continue searching for the client, we already found it
                        break;
                    }
                }
            }
            if (client != null && userId != null)
            {
                client.Peer = null;
                client.Status = PlayerStatus.Disconnected;

                BroadcastClientUpdate(client, userId);

                EvaluateGameComplete();
            }

            if (_shutdownMode == ShutdownMode.NoPlayerLeft)
            {
                if (!_clients.Values.Any(c => c.Status != PlayerStatus.Disconnected))
                {
                    var _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000 * 60);
                        if (!_clients.Values.Any(c => c.Status != PlayerStatus.Disconnected))
                        {
                            await CloseGameServer();
                        }
                    });
                }
            }
            return Task.CompletedTask;
        }

        private static ValueTask CloseGameServer() => ValueTask.CompletedTask;

        public Task Reset()
        {
            foreach (var client in _clients.Values)
            {
                client.Reset();
            }

            _gameCompleteExecuted = false;

            return Task.FromResult(0);
        }

        public async Task<Action<Stream, ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer)
        {
            if (this._status != ServerStatus.Started)
            {
                throw new ClientException($"Unable to post result before game session start. Server status is {this._status}");
            }
            var userId = await GetUserId(remotePeer);
            if (userId != null)
            {

                var memStream = new MemoryStream();
                inputStream.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);
                _clients[userId].ResultData = memStream;

                EvaluateGameComplete();

                var tcs = _clients[userId].GameCompleteTcs;
                if(tcs !=null)
                {
                    return await tcs.Task;
                }
                else
                {
                    static void NoOp(Stream stream,ISerializer serializer) { };
                    return NoOp;
                }
               
            }
            else
            {
                throw new ClientException("unauthorized?reason=publicGame");
            }

        }

        private async Task EvaluateGameComplete(Stream inputStream)
        {
            Debug.Assert(_config != null);

            var ctx = new GameSessionCompleteCtx(this, _scene, _config, new[] { new GameSessionResult("", null, inputStream) }, _clients.Keys);
            await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.GameSessionCompleted(ctx), ex =>
                {
                    _logger.Log(LogLevel.Error, "gameSession", "An error occured while running gameSession.GameSessionCompleted event handlers", ex);
                });
            }

            // FIXME: Temporary workaround to issue where disconnections cause large increases in CPU/Memory usage
            //await Task.WhenAll(_scene.RemotePeers.Select(user => user.Disconnect("Game complete")));

            RaiseGameCompleted();
            await _scene.KeepAlive(TimeSpan.Zero);
        }

        private void RaiseGameCompleted()
        {
            _gameCompleteCts.Cancel();
            OnGameSessionCompleted?.Invoke();
        }


        public string GameSessionId => _scene.Id;

        private bool _gameCompleteExecuted = false;

        public event Action? OnGameSessionCompleted;

        private void EvaluateGameComplete()
        {
            Debug.Assert(_config != null);
            lock (this)
            {
                if (!_gameCompleteExecuted && _clients.Values.All(c => c.ResultData != null || c.Peer == null))//All remaining clients sent their data
                {
                    _gameCompleteExecuted = true;

                    var ctx = new GameSessionCompleteCtx(this, _scene, _config, _clients.Select(kvp => new GameSessionResult(kvp.Key, kvp.Value.Peer, kvp.Value.ResultData?? new MemoryStream())), _clients.Keys);

                    async Task runHandlers()
                    {
                        await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                        {
                            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.GameSessionCompleted(ctx), ex =>
                            {
                                _logger.Log(LogLevel.Error, "gameSession", "An error occured while running gameSession.GameSessionCompleted event handlers", ex);
                                foreach (var client in _clients.Values)
                                {
                                    client.GameCompleteTcs?.TrySetException(ex);
                                }
                            });
                        }

                        foreach (var client in _clients.Values)
                        {
                            client.GameCompleteTcs?.TrySetResult(ctx.ResultsWriter);
                        }

                        await Task.Delay(5000);

                        // Update : Do not disconnect players to allow them to restart a game.
                        // By uncommenting the next line, you can encounter RPC failures if EvaluateGameComplete was called from an RPC called by the client (for example postResults).
                        //await Task.WhenAll(_scene.RemotePeers.Select(user => user.Disconnect("gamesession.completed")));

                        RaiseGameCompleted();

                        await _scene.KeepAlive(TimeSpan.Zero);
                    }

                    _ = runHandlers();
                }
            }
        }

        public async Task<string?> CreateP2PToken(string sessionId)
        {
            var hostPeer = await GetServerTcs().Task;
            if (sessionId == hostPeer.SessionId)
            {
                return null;
            }
            else
            {
                return await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(hostPeer.SessionId, _scene.Id);
            }
        }

        public async Task UpdateShutdownMode(ShutdownModeParameters shutdown)
        {
            if (shutdown.shutdownMode == ShutdownMode.SceneShutdown)
            {
                _shutdownMode = shutdown.shutdownMode;
                _shutdownDate = await _scene.KeepAlive(new TimeSpan(0, 0, shutdown.keepSceneAliveFor));
            }
        }

        public bool IsHost(string sessionId)
        {
            if (!GetServerTcs().Task.IsCompleted)
            {
                return false;
            }
            return sessionId == GetServerTcs().Task.Result.SessionId;
        }

        public ValueTask DisposeAsync()
        {
            _gameCompleteCts?.Dispose();
            return CloseGameServer();
        }

        public GameSessionConfigurationDto GetGameSessionConfig()
        {
            Debug.Assert(_config != null);
            return new GameSessionConfigurationDto { Teams = _config.TeamsList, Parameters = _config.Parameters, UserIds = _config.UserIds, HostUserId = _config.HostUserId };
        }



        public void OnConfigurationChanged()
        {
            ApplySettings();
        }

        public void UpdateGameSessionConfig(Action<GameSessionConfiguration> gameSessionConfigUpdater)
        {
            Debug.Assert(_config != null);
            if (gameSessionConfigUpdater is null)
            {
                throw new ArgumentNullException(nameof(gameSessionConfigUpdater));
            }

            gameSessionConfigUpdater(_config);
        }
    }
}
