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
using System.Diagnostics.CodeAnalysis;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using MsgPack.Serialization;
using System.Reactive.Subjects;
using Docker.DotNet.Models;
using System.Collections.Immutable;

namespace Stormancer.Server.Plugins.GameSession
{

    internal class GameSessionState
    {
        private readonly ISceneHost scene;

        public GameSessionState(ISceneHost scene)
        {
            this.scene = scene;
        }

        public GameSessionTemplateConfiguration GetTemplateConfiguration() => GameSessionsExtensions.GetConfig(scene.Template);

        public bool DirectConnectionEnabled() => GetTemplateConfiguration().PeerDirectConnectionEnabled(scene);

        public bool UseGameServer() => GetTemplateConfiguration().GameServerConfig.useGameServerGetter(scene);
        public string? GameServerPool() => GetTemplateConfiguration().GameServerConfig.gameServerPoolIdGetter(scene);
        public bool IsServerPersistent() => GetTemplateConfiguration().GameServerConfig.isServerPersistentGetter(scene);
        public TimeSpan GameServerStartTimeout() => GetTemplateConfiguration().GameServerConfig.serverStartTimeoutGetter(scene);
    }
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

    /// <summary>
    /// Message sent to peers to provide infos about the game session host and connectivity. 
    /// </summary>
    public class HostInfosMessage
    {
        /// <summary>
        /// If P2P enabled, contains the connection token to the host.
        /// </summary>
        [MessagePackMember(0)]
        public string? P2PToken { get; set; }

        /// <summary>
        /// True if the receiving peer is the host.
        /// </summary>
        [MessagePackMember(1)]
        public bool IsHost { get; set; }

        /// <summary>
        /// Session id of the host.
        /// </summary>
        [MessagePackMember(2)]
        public string? HostSessionId { get; set; }
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
        public int MaxClientsConnected { get; private set; } = 0;

        // Constant variable
        private const string LOG_CATEOGRY = "gamesession";
        private const string P2P_TOKEN_ROUTE = "player.p2ptoken";
        private const string ALL_PLAYER_READY_ROUTE = "players.allReady";

        // Stormancer object

        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly GameSessionState state;
        private readonly GameSessionAnalyticsWorker _analytics;
        private readonly ISceneHost _scene;
        private readonly IEnvironment _environment;
        private readonly RpcService _rpc;
        private readonly GameSessionsRepository _repository;
        private readonly ISerializer _serializer;

        private TimeSpan _gameSessionTimeout = TimeSpan.MaxValue;
        private GameSessionConfiguration? _config;
        private readonly CancellationTokenSource _sceneCts = new();


        private readonly ConcurrentDictionary<string, Client> _clients = new();
        private ServerStatus _status = ServerStatus.WaitingPlayers;
        // A source that is canceled when the game session is complete
        private readonly CancellationTokenSource _gameCompleteCts = new();

        //set to true to indicate a player connected to the session at least once.
        private bool _playerConnectedOnce = false;

        private string? _p2pToken;


        private readonly object _lock = new();
        private readonly ManagementClientProvider _management;
        private TaskCompletionSource<IScenePeerClient>? _serverPeer = null;
        private ShutdownMode _shutdownMode;
        private DateTime _shutdownDate;

        public GameSessionService(
            GameSessionState state,
            GameSessionAnalyticsWorker analyticsWorker,
            ISceneHost scene,
            IConfiguration configuration,
            IEnvironment environment,
            ManagementClientProvider management,
            ILogger logger,
            RpcService rpc,
            GameSessionsRepository repository,
            ISerializer serializer)
        {
            _management = management;
            this.state = state;
            _analytics = analyticsWorker;
            _scene = scene;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;

            _rpc = rpc;
            _repository = repository;
            _serializer = serializer;


            ApplySettings();

            analyticsWorker.AddGameSession(this);
            scene.Shuttingdown.Add(args =>
            {
                analyticsWorker.RemoveGameSession(this);
                _repository.RemoveGameSession(this);
                _sceneCts.Cancel();
                return Task.CompletedTask;
            });



            _reservationCleanupTimer = new Timer((_) => _ = ReservationCleanupCallback(null), null, 5000, 5000);
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
                return (await GetSessionAsync(peer))?.User?.Id;
            }
        }

        private async Task<Session?> GetSessionAsync(IScenePeerClient peer)
        {
            await using var scope = _scene.CreateRequestScope();
            var sessions = scope.Resolve<IUserSessions>();
            return await sessions.GetSession(peer, CancellationToken.None);
        }

        public async Task SetPlayerReady(IScenePeerClient peer, string customData)
        {
            try
            {

                await using var dr = _scene.CreateRequestScope();
                var sessions = dr.Resolve<IUserSessions>();

                var session = await sessions.GetSession(peer, CancellationToken.None);

                //Peer not authd.
                if (session is null)
                {
                    return;
                }

                if (IsDedicatedServer(session))
                {

                    await SignalHostReady(peer, null);
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
                    BroadcastClientUpdate(currentClient, user, customData);
                }

                await CheckAllPlayersReady();

                if (IsHost(peer.SessionId) && _p2pToken == null)
                {
                    await SignalHostReady(peer, session.User.Id);

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

        public async Task SetPeerFaulted(IScenePeerClient peer)
        {

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

        public async Task OnPeerConnecting(IScenePeerClient peer)
        {


            if (peer == null)
            {
                throw new ArgumentNullException(nameof(peer));
            }
            var session = await GetSessionAsync(peer);
            var user = session?.User?.Id;
            if (session == null || user == null)
            {
                throw new ClientException("notAuthenticated");
            }

            if (_config == null)
            {
                throw new InvalidOperationException("Game session plugin configuration missing in scene instance metadata. Please check the scene creation process.");
            }

            if (IsDedicatedServer(session))
            {
                return;
            }

            lock (syncRoot)
            {

                if (!_config.Public && !_config.UserIds.Any(u => u == user))
                {
                    throw new ClientException("You are not authorized to join this game.");
                }
            }

            var client = new Client(peer);
            lock (_clients)
            {
                if (!_clients.TryAdd(user, client))
                {
                    if (_clients.TryGetValue(user, out var alreadyConnectedClient))
                    {
                        if (!_clients.TryUpdate(user, client, alreadyConnectedClient))
                        {
                            throw new ClientException("Failed to update peer associated with user.");
                        }
                    }
                }
            }
        }

        public async Task OnPeerConnectionRejected(IScenePeerClient peer)
        {
            lock (_clients)
            {
                var client = _clients.FirstOrDefault(kvp => kvp.Value.Peer == peer);
                if (client.Key != null)
                {
                    _clients.TryRemove(client.Key, out _);
                }
            }
            await Task.CompletedTask;
        }

        private async Task SignalHostReady(IScenePeerClient peer, string? userId)
        {
            var sessionId = peer.SessionId;
            GetServerTcs().TrySetResult(peer);
            _status = ServerStatus.Started;
            await SendP2PToken(Enumerable.Repeat(sessionId, 1), true, "", default);
            if (state.DirectConnectionEnabled())
            {
                _p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(sessionId, _scene.Id);

                await SendP2PToken(_scene.RemotePeers.Where(p => p.SessionId != sessionId).Select(p => p.SessionId), false, _p2pToken, sessionId);
            }
            else
            {
                _p2pToken = "";
            }


            if (userId == null)
            {
                var playerUpdate = new PlayerUpdate { IsHost = true, Status = (byte)PlayerStatus.Ready, UserId = userId ?? "server" };
                await _scene.Send(new MatchArrayFilter(_scene.RemotePeers.Where(p => p.SessionId != sessionId)), "player.update", s => _serializer.Serialize(playerUpdate, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
            }

            if (_server != null)
            {
                var serverCtx = new ServerReadyContext(peer, _server);


                await using (var serverReadyscope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                {
                    await serverReadyscope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnServerReady(serverCtx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.OnServerReady event handlers", ex);
                    });
                }
            }

        }

        public bool IsDedicatedServer(Session session)
        {

            return session.platformId.Platform.StartsWith(DedicatedServerAuthProvider.PROVIDER_NAME);
        }

        public Task SendP2PToken(IEnumerable<SessionId> target, bool isHost, string token, SessionId hostSessionId)
        {
            return _scene.Send(new MatchArrayFilter(target), P2P_TOKEN_ROUTE, s => _serializer.Serialize(new HostInfosMessage { HostSessionId = hostSessionId.IsEmpty() ? "" : hostSessionId.ToString(), IsHost = isHost, P2PToken = token }, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
        }

        public async Task OnPeerConnected(IScenePeerClient peer)
        {
            Debug.Assert(_config != null);

            await using var dr = _scene.CreateRequestScope();
            var sessions = dr.Resolve<IUserSessions>();

            var session = await sessions.GetSession(peer, CancellationToken.None);

            if (session is null)
            {
                return;
            }

            var isDedicatedServer = IsDedicatedServer(session);
            //Is authenticated as a dedicated server
            if (isDedicatedServer)
            {
                GetServerTcs().TrySetResult(peer);

                await SendP2PToken(Enumerable.Repeat(peer.SessionId, 1), true, "", default);

                return;
            }

            _playerConnectedOnce = true;

            var client = _clients.First(client => client.Value.Peer == peer);
            client.Value.Status = PlayerStatus.Connected;
            if (!_config.Public)
            {
                BroadcastClientUpdate(client.Value, client.Key);
            }

            var serverFound = await TryStart();



            var userId = client.Key;


            _analytics.PlayerJoined(userId, peer.SessionId.ToString(), _scene.Id);



            //Check if the gameSession is Dedicated or listen-server            

            // If the host is not defined a P2P was sent with "" to notify client is host.
            _logger.Log(LogLevel.Trace, "gamesession", $"Gamesession {_scene.Id} evaluating {userId} as host (expected host :{_config.HostUserId})", new { });
            if (!serverFound && (string.IsNullOrEmpty(_config.HostUserId) || _config.HostUserId == userId))
            {
                _config.HostUserId = userId;
                if (GetServerTcs().TrySetResult(peer))
                {
                    _logger.Log(LogLevel.Debug, LOG_CATEOGRY, "Host defined and connecting", userId);
                    await SendP2PToken(Enumerable.Repeat(peer.SessionId, 1), true, "", default);

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
                    if (_clients.TryGetValue(uId, out var currentClient))
                    {
                        var isHost = GetServerTcs().Task.IsCompleted && GetServerTcs().Task.Result.SessionId == currentClient.Peer?.SessionId;
                        peer.Send("player.update",
                            new PlayerUpdate { UserId = uId, IsHost = isHost, Status = (byte)currentClient.Status, Data = currentClient.FaultReason ?? "" },
                            PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
                    }
                }
            }
            if (_status == ServerStatus.Started)
            {

                if (_p2pToken == null && GetServerTcs().Task.IsCompleted)
                {
                    if (state.DirectConnectionEnabled())
                    {

                        _p2pToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken((await GetServerTcs().Task).SessionId, _scene.Id);
                    }
                    else
                    {
                        _p2pToken = "";
                    }
                }


                if (_p2pToken != null && state.DirectConnectionEnabled())
                {
                    await SendP2PToken(Enumerable.Repeat(peer.SessionId, 1), false, _p2pToken, (await GetServerTcs().Task).SessionId);
                }

                var serverUpdate = new PlayerUpdate { IsHost = true, Status = (byte)PlayerStatus.Ready, UserId = "server" };
                await _scene.Send(new MatchPeerFilter(peer), "player.update", s => _serializer.Serialize(serverUpdate, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
            }

            var playerConnectedCtx = new ClientConnectedContext(this, new PlayerPeer(peer, new Player(peer.SessionId.ToString(), userId)), _config.HostUserId == userId);
            await using var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag);
            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                h => h.OnClientConnected(playerConnectedCtx),
                ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing OnClientConnected event", ex));


            var count = _clients.Count;
            if (MaxClientsConnected < count)
            {
                MaxClientsConnected = count;
            }
        }

        private Task<bool>? _serverStartTask = null;

        public Task<bool> TryStart()
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

        private async Task<bool> Start()
        {
            Debug.Assert(_config != null);
            _analytics.StartGamesession(this);
            var ctx = new GameSessionContext(this._scene, _config, this);
            _logger.Log(LogLevel.Info, "gamesession.startup", "Starting up gamesession.", new { id = this.GameSessionId }, this.GameSessionId);

            await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
            {
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(h => h.GameSessionStarting(ctx), ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing GameSessionStarting event", ex));
            }

            _logger.Log(LogLevel.Info, "gamesession.startup", "Ran GameSessionStarting event handlers.", new { id = this.GameSessionId }, this.GameSessionId);

            _logger.Log(LogLevel.Info, "gamesession.startup", "Creating Gamesession server.", new { id = this.GameSessionId }, this.GameSessionId);
            var poolId = state.GameServerPool();


            if (poolId != null)
            {
                await using (var scope = _scene.CreateRequestScope())
                {
                    var pools = scope.Resolve<ServerPoolProxy>();

                    if (!state.IsServerPersistent())
                    {
                        _scene.Disconnected.Add(async (args) =>
                        {
                            if (this._server != null)
                            {

                                //If the only peer remaining is the server, close it and destroy the gamesession.
                                if (!_scene.RemotePeers.Any(p => p.SessionId != _server.GameServerSessionId))
                                {
                                    _gameCompleteCts.Cancel();
                                    await pools.CloseServer(_server.GameServerId, CancellationToken.None);
                                    _repository.RemoveGameSession(this);
                                    _scene.Shutdown("gamesession.empty");

                                }
                            }
                            else
                            {
                                if (!_scene.RemotePeers.Any())
                                {
                                    _gameCompleteCts.Cancel();
                                    _repository.RemoveGameSession(this);
                                    _scene.Shutdown("gamesession.empty");
                                }
                            }


                        });

                    }

                    _logger.Log(LogLevel.Info, "gamesession.startup", "starting gameserver.", new { id = this.GameSessionId }, this.GameSessionId);

                    _server = await pools.TryStartGameServer(poolId, GameSessionId, _config, _gameCompleteCts.Token);

                    if (_server != null)
                    {
                        _logger.Log(LogLevel.Info, "gamesession.startup", "started gameserver.", new { id = this.GameSessionId, _server.GameServerId, _server.GameServerSessionId }, this.GameSessionId);
                        if (!state.IsServerPersistent())
                        {
                            _ = _scene.RunTask(async ct =>
                            {
                                await Task.Delay(1000 * 60 * 5);
                                if (!_playerConnectedOnce)
                                {
                                    if (_server != null)
                                    {
                                        await pools.CloseServer(_server.GameServerId, CancellationToken.None);
                                    }

                                    _gameCompleteCts.Cancel();
                                    _repository.RemoveGameSession(this);
                                    _scene.Shutdown("gamesession.empty");
                                }
                            });
                        }
                    }
                    else
                    {
                        _logger.Log(LogLevel.Info, "gamesession.startup", "No gameserver found, using P2P mode.", new { id = this.GameSessionId, }, this.GameSessionId);
                    }
                }
            }

            if (poolId != null)
            {
                this.SetDimension("pool", poolId);
            }
            this.SetDimension("hostType", _server != null ? "server" : "client");
            SetDimension("gamefinder", _config?.GameFinder ?? "");
            SetDimension("template", _scene.Template);
            _repository.AddGameSession(this);

            return _server != null;
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

        public async Task OnPeerDisconnecting(IScenePeerClient peer)
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

            _analytics.PlayerLeft(peer.SessionId.ToString(), this._scene.Id);


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
                        else
                        {
                            kvp.Value.Peer = null;
                            kvp.Value.Status = PlayerStatus.Disconnected;
                        }

                        // no need to continue searching for the client, we already found it
                        break;
                    }
                }
            }

            if (client != null && userId != null)
            {
                var ctx = new ClientLeavingContext(this, new PlayerPeer(peer, new Player(peer.SessionId.ToString(), userId)), _config.HostUserId == userId);
                await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                {
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnClientLeaving(ctx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.OnClientLeaving event handlers", ex);
                    });
                }

                client.Peer = null;
                client.Status = PlayerStatus.Disconnected;

                BroadcastClientUpdate(client, userId);

                await EvaluateGameComplete();
            }


        }

        private async ValueTask CloseGameServer()
        {
            if (GetServerTcs().Task.IsCompletedSuccessfully)
            {

                var poolId = state.GameServerPool();
                Debug.Assert(poolId != null);

                if (state.UseGameServer())
                {
                    await using var scope = _scene.CreateRequestScope();
                    var pools = scope.Resolve<ServerPoolProxy>();
                    if (_server != null)
                    {
                        await pools.CloseServer(_server.GameServerId, CancellationToken.None);
                    }
                }
            }
        }

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

                await EvaluateGameComplete();

                var tcs = _clients[userId].GameCompleteTcs;
                if (tcs != null)
                {
                    return await tcs.Task;
                }
                else
                {
                    static void NoOp(Stream stream, ISerializer serializer) { };
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

        public DateTime CreatedOn { get; } = DateTime.UtcNow;

        private object _syncRoot = new object();
        private Dictionary<string, string> _dimensions = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> Dimensions
        {
            get
            {
                lock (_syncRoot)
                {
                    return _dimensions.ToImmutableDictionary();
                }
            }
        }

        public void SetDimension(string dimension, string value)
        {
            lock (_syncRoot)
            {
                _dimensions[dimension] = value;
            }
        }

        private bool _gameCompleteExecuted = false;

        public event Action? OnGameSessionCompleted;

        private async Task EvaluateGameComplete()
        {
            Debug.Assert(_config != null);
            var ctx = new GameSessionCompleteCtx(this, _scene, _config, _clients.Select(kvp => new GameSessionResult(kvp.Key, kvp.Value.Peer, kvp.Value.ResultData ?? new MemoryStream())), _clients.Keys);


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

                async Task DelayAndComplete()
                {
                    await Task.Delay(5000);

                    // Update : Do not disconnect players to allow them to restart a game.
                    // By uncommenting the next line, you can encounter RPC failures if EvaluateGameComplete was called from an RPC called by the client (for example postResults).
                    //await Task.WhenAll(_scene.RemotePeers.Select(user => user.Disconnect("gamesession.completed")));

                    RaiseGameCompleted();

                    await _scene.KeepAlive(TimeSpan.Zero);
                };

                _ = DelayAndComplete();

            }

            bool shouldRunHandlers = false;
            lock (this)
            {
                if (!_gameCompleteExecuted && _clients.Values.All(c => c.ResultData != null || c.Peer == null))//All remaining clients sent their data
                {
                    _gameCompleteExecuted = true;


                    shouldRunHandlers = true;

                }
            }

            if (shouldRunHandlers)
            {
                await runHandlers();
            }
        }

        public async Task<HostInfosMessage> CreateP2PToken(SessionId sessionId)
        {
            if (!state.DirectConnectionEnabled())
            {
                return new HostInfosMessage { IsHost = false, HostSessionId = null, P2PToken = null };
            }

            var hostPeer = await GetServerTcs().Task;
            if (sessionId == hostPeer.SessionId)
            {
                return new HostInfosMessage { IsHost = true, HostSessionId = sessionId.ToString() };
            }
            else
            {
                return new HostInfosMessage { IsHost = false, HostSessionId = hostPeer.SessionId.ToString(), P2PToken = await _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(hostPeer.SessionId, _scene.Id) };
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

        public bool IsHost(SessionId sessionId)
        {
            if (!GetServerTcs().Task.IsCompleted)
            {
                return false;
            }
            try
            {
                return sessionId == GetServerTcs().Task.Result.SessionId;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _gameCompleteCts?.Dispose();
            _reservationCleanupTimer?.Dispose();
            GetServerTcs().TrySetCanceled();
            await CloseGameServer();
        }

        public GameSessionConfigurationDto? GetGameSessionConfig()
        {
            if (_config == null)
            {
                return null;
            }
            else
            {
                return new GameSessionConfigurationDto { Teams = _config.TeamsList, Parameters = _config.Parameters, UserIds = _config.UserIds, HostUserId = _config.HostUserId, GameFinder = _config.GameFinder };
            }

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

        private object syncRoot = new object();

        #region Reservations
        public async Task<GameSessionReservation?> CreateReservationAsync(Team team, JObject args, CancellationToken cancellationToken)
        {
            if (_config == null)
            {
                return null;
            }
            await using var scope = _scene.CreateRequestScope();


            foreach (var player in team.AllPlayers)
            {
                var playerTeam = FindPlayerTeam(player.UserId);
                if (playerTeam != null && playerTeam.TeamId != team.TeamId)
                {
                    //Player already in another team, we can't make new reservation.
                    return null;
                }
            }
            var reservationState = new ReservationState();
            var ctx = new CreatingReservationContext(team, args, reservationState.ReservationId);

            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                h => h.OnCreatingReservation(ctx),
                ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing OnCreatingReservation event", ex));


            if (ctx.Accept)
            {
                lock (syncRoot)
                {
                    var currentTeam = _config.Teams.FirstOrDefault(t => t.TeamId == team.TeamId);

                    if (currentTeam != null)
                    {
                        foreach (var party in team.Parties)
                        {
                            var currentParty = currentTeam.Parties.FirstOrDefault(p => p.PartyId == party.PartyId);
                            if (currentParty != null)
                            {
                                foreach (var player in party.Players)
                                {
                                    currentParty.Players.TryAdd(player.Key, player.Value);
                                    reservationState.UserIds.Add(player.Key);
                                }
                            }
                            else
                            {
                                currentTeam.Parties.Add(party);
                                reservationState.UserIds.AddRange(party.Players.Keys);
                            }
                        }
                    }
                    else
                    {
                        _config.Teams.Add(team);
                        reservationState.UserIds.AddRange(team.AllPlayers.Select(p => p.UserId));
                    }
                    _reservationStates.TryAdd(reservationState.ReservationId, reservationState);
                }
                var createdCtx = new CreatedReservationContext(team, args, reservationState.ReservationId);

                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                    h => h.OnCreatedReservation(createdCtx),
                    ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnCreatedReservation event", ex));

                return new GameSessionReservation { ReservationId = reservationState.ReservationId.ToString(), ExpiresOn = reservationState.ExpiresOn };
            }
            else
            {
                return null;
            }


        }
        public async Task CancelReservationAsync(string id, CancellationToken cancellationToken)
        {
            if (_reservationStates.TryRemove(Guid.Parse(id), out var reservationState))
            {
                var players = new List<(string TeamId, Player Player)>();
                lock (syncRoot)
                {

                    foreach (var userId in reservationState.UserIds)
                    {
                        if (TryRemoveUserFromConfig(userId, out var teamId, out var player))
                        {
                            players.Add((teamId, player));
                        }
                    }
                }

                await using var scope = _scene.CreateRequestScope();

                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                   h => h.OnReservationCancelled(new ReservationCancelledContext(reservationState.ReservationId, players)),
                   ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing OnReservationCancelled event", ex));

            }

        }

        private bool _reservationCleanupRunning = false;
        private async Task ReservationCleanupCallback(object? userState)
        {
            if (!_reservationCleanupRunning)
            {
                _reservationCleanupRunning = true;
                try
                {
                    foreach (var reservationState in _reservationStates.Values)
                    {
                        if (reservationState.ExpiresOn < DateTime.UtcNow)
                        {
                            var players = new List<(string TeamId, Player Player)>();
                            foreach (var userId in reservationState.UserIds)
                            {
                                if (TryRemoveUserFromConfig(userId, out var teamId, out var player))
                                {
                                    players.Add((teamId, player));
                                }
                            }

                            await using var scope = _scene.CreateRequestScope();

                            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                               h => h.OnReservationCancelled(new ReservationCancelledContext(reservationState.ReservationId, players)),
                               ex => _logger.Log(LogLevel.Error, "gameSession", "An error occured while executing OnReservationCancelled event", ex));
                        }


                    }
                }
                finally
                {
                    _reservationCleanupRunning = false;
                }
            }


        }

        private bool TryRemoveUserFromConfig(string userId, [NotNullWhen(true)] out string? teamId, [NotNullWhen(true)] out Player? player)
        {
            if (!_clients.ContainsKey(userId) && _config != null)
            {
                foreach (var team in _config.Teams)
                {
                    foreach (var party in team.Parties)
                    {
                        if (party.Players.TryGetValue(userId, out player))
                        {
                            party.Players.Remove(userId);
                            if (!party.Players.Any())
                            {
                                team.Parties.Remove(party);
                            }
                            if (!team.Parties.Any())
                            {
                                _config.Teams.Remove(team);
                            }
                            teamId = team.TeamId;
                            return true;
                        }

                    }
                }
            }
            teamId = null;
            player = null;
            return false;

        }

        private class ReservationState
        {
            public Guid ReservationId { get; } = Guid.NewGuid();
            public DateTime ExpiresOn { get; set; } = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            public List<string> UserIds { get; set; } = new List<string>();
        }

        private ConcurrentDictionary<Guid, ReservationState> _reservationStates = new ConcurrentDictionary<Guid, ReservationState>();
        private Timer _reservationCleanupTimer;
        private GameServer? _server;

        private Team? FindPlayerTeam(string userId)
        {
            if (_config == null)
            {
                return null;
            }
            return _config.Teams.FirstOrDefault(t => t.AllPlayers.Any(p => p.UserId == userId));
        }

        public Task<string> CreateP2PToken(SessionId callerSessionId, SessionId remotePeerSessionId)
        {
            return _scene.DependencyResolver.Resolve<IPeerInfosService>().CreateP2pToken(remotePeerSessionId, _scene.Id);
        }
        #endregion

    }
}
