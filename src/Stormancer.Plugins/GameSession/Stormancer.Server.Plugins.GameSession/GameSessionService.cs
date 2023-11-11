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
using SmartFormat.Utilities;
using Microsoft.AspNetCore.Components.Web;

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

    /// <summary>
    /// A client in the game session.
    /// </summary>
    public class Client
    {
        internal Client(IScenePeerClient peer, SessionId sessionId, Session session)
        {
            Peer = peer;
            SessionId = sessionId;
            Reset();
            Status = PlayerStatus.NotConnected;
            Session = session;
        }

        internal void Reset()
        {
            GameCompleteTcs?.TrySetCanceled();
            GameCompleteTcs = new TaskCompletionSource<Action<Stream, ISerializer>>();
            ResultData = null;
        }

        /// <summary>
        /// Gets or sets the peer representing the client.
        /// </summary>
        public IScenePeerClient? Peer { get; set; }

        /// <summary>
        /// Gets or sets the client's session id.
        /// </summary>
        public SessionId SessionId { get; }

        /// <summary>
        /// Gets or sets the client's session, if the client is connected to the game session.
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Gets or sets the client's results as sent by them.
        /// </summary>
        public Stream? ResultData { get; set; }

        /// <summary>
        /// Gets or sets the client's status.
        /// </summary>
        public PlayerStatus Status { get; set; }

        /// <summary>
        /// If the client is faulted, gets or sets the reason.
        /// </summary>

        public string? FaultReason { get; set; }

        /// <summary>
        /// Gets a completion event triggered when the results of the clients were sent.
        /// </summary>
        public TaskCompletionSource<Action<Stream, ISerializer>>? GameCompleteTcs { get; private set; }
    }

    internal class GameSessionService : IGameSessionService, IConfigurationChangedEventHandler, IAsyncDisposable
    {

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
        private readonly GameSessionEventsRepository _events;
        private TimeSpan _gameSessionTimeout = TimeSpan.MaxValue;
        private GameSessionConfiguration? _config;
        private readonly CancellationTokenSource _sceneCts = new();


        private readonly ConcurrentDictionary<string, Client> _clients = new();
        private ServerStatus _status = ServerStatus.WaitingPlayers;
        // A source that is canceled when the game session is complete
        private CancellationTokenSource? _gameCompleteCts = new();

        //set to true to indicate a player connected to the session at least once.
        private bool _playerConnectedOnce = false;

        private string? _p2pToken;


        private readonly object _lock = new();
        private readonly ManagementClientProvider _management;
        private TaskCompletionSource<IScenePeerClient>? _serverPeer = null;
        private ShutdownMode _shutdownMode;
        private DateTime _shutdownDate;

        private DateTime _lastServerKeepAlive;

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
            ISerializer serializer, GameSessionEventsRepository events
            )
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
            _events = events;
            ApplySettings();

            events.PostEventAsync(new GameSessionEvent() { GameSessionId = scene.Id, Type = "gamesessionCreated" });
            analyticsWorker.AddGameSession(this);
            scene.Shuttingdown.Add(async args =>
            {
                events.PostEventAsync(new GameSessionEvent() { GameSessionId = scene.Id, Type = "gamesessionShutdown" });
                await this.EvaluateGameComplete(true);
                analyticsWorker.RemoveGameSession(this);
                _repository.RemoveGameSession(this);
                _sceneCts.Cancel();

                await using var scope = _scene.CreateRequestScope();
                var ctx = new GameSessionShutdownContext(this);
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnGameSessionShutdown(ctx), ex =>
                {
                    _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.OnGameSessionShutdown event handlers", ex);
                });

            });

            _scene.RunTask(async cancellationToken =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await ReservationCleanupCallback(null);

                        await UpdateServerKeepAliveAsync(cancellationToken);
                        await EvaluateShutdown();
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "gamesession", "An error occurred while running the game session cleanup method.", ex);
                    }
                    await timer.WaitForNextTickAsync(cancellationToken);
                }
            });


        }

        private Task UpdateServerKeepAliveAsync(CancellationToken cancellationToken)
        {
            if (_server != null && _lastServerKeepAlive < DateTime.UtcNow + TimeSpan.FromSeconds(GameSessionPlugin.SERVER_KEEPALIVE_INTERVAL_SECONDS))
            {
                _lastServerKeepAlive = DateTime.UtcNow;
                return ServerKeepAliveAsync(cancellationToken);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private async Task ServerKeepAliveAsync(CancellationToken cancellationToken)
        {
            await using (var scope = _scene.CreateRequestScope())
            {
                var pools = scope.Resolve<ServerPoolProxy>();
                
                try
                {
                    if (_server != null)
                    {
                        await pools.KeepAlive(_server.GameServerId.PoolId, _server.GameServerId.Id, cancellationToken);
                    }

                }
                catch (Exception)
                {

                }
            }
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

                _logger.Log(LogLevel.Info, _scene.Id, "Set player ready", new { peer.SessionId, session = session != null });

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
                            _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.OnClientReady event handlers", ex);
                        });
                    }
                    BroadcastClientUpdate(currentClient, user, peer.SessionId, customData);
                }

                await CheckAllPlayersReady();

                if (IsHost(peer.SessionId) && _p2pToken == null)
                {
                    await SignalHostReady(peer, session.User!.Id);

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

        private void BroadcastClientUpdate(Client client, string userId, SessionId sessionId, string? data = null)
        {
            Debug.Assert(_config != null);

            _scene.Broadcast("player.update", new PlayerUpdate { UserId = userId, Status = (byte)client.Status, Data = data ?? "", IsHost = (HostSessionId == sessionId) }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
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
            if (!TryResetShutdown())
            {
                throw new ClientException("sceneShutdown");

            }

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


            }

            var client = new Client(peer, peer.SessionId, session);
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
            _logger.Log(LogLevel.Info, _scene.Id, "Signal host ready", new { peer.SessionId, userId, server = _server != null });

            if (_server == null)
            {
                await TryStart();
            }

            var sessionId = peer.SessionId;
            GetServerTcs().TrySetResult(peer);
            _status = ServerStatus.Started;
            //await SendP2PToken(Enumerable.Repeat(sessionId, 1), true, "", default);
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
                _logger.Log(LogLevel.Info, _scene.Id, "run OnServerReady", new { peer.SessionId, userId });
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
            if (!TryResetShutdown())
            {
                await peer.Disconnect("sceneShutdown");
                return;
            }

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

            var client = _clients.FirstOrDefault(client => client.Value.SessionId == peer.SessionId);
            if (client.Value == null)
            {
                await peer.Disconnect("noClient");
                throw new InvalidOperationException($"No client found for player {peer.SessionId}");
            }
            if (client.Value.Peer == null)
            {
                //Peer already disconnected.
                return;
            }


            client.Value.Status = PlayerStatus.Connected;
            if (!_config.Public)
            {
                BroadcastClientUpdate(client.Value, client.Key, client.Value.Session.SessionId);
            }

            var serverFound = await TryStart();



            var userId = client.Key;


            _analytics.PlayerJoined(userId, peer.SessionId.ToString(), _scene.Id);



            //Check if the gameSession is Dedicated or listen-server            

            // If the host is not defined a P2P was sent with "" to notify client is host.
            _logger.Log(LogLevel.Trace, "gamesession", $"Gamesession {_scene.Id} evaluating {userId} as host (expected host :{_config.HostSessionId})", new { });
            if (HostSessionId.IsEmpty() && !serverFound && ((string.IsNullOrEmpty(_config.HostSessionId)) || _config.HostSessionId == peer.SessionId.ToString()))
            {
                HostSessionId = peer.SessionId;
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

            var playerConnectedCtx = new ClientConnectedContext(this, new PlayerPeer(peer.SessionId, new Player(peer.SessionId, userId)), HostSessionId == peer.SessionId);
            await using var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag);
            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                h => h.OnClientConnected(playerConnectedCtx),
                ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnClientConnected event", ex));


            var count = _clients.Count;
            if (MaxClientsConnected < count)
            {
                MaxClientsConnected = count;
            }
        }

        public SessionId HostSessionId { get; private set; }

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
            try
            {
                Debug.Assert(_config != null);
                _analytics.StartGamesession(this);
                var ctx = new GameSessionContext(this._scene, _config, this);

                await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                {
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(h => h.GameSessionStarting(ctx), ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing GameSessionStarting event", ex));
                }


                var poolId = state.GameServerPool();


                if (poolId != null)
                {
                    await using (var scope = _scene.CreateRequestScope())
                    {
                        var pools = scope.Resolve<ServerPoolProxy>();
                        if (_gameCompleteCts == null)
                        {
                            return false;
                        }
                        try
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _gameCompleteCts.Token);
                            _server = await pools.TryStartGameServer(poolId, GameSessionId, _config, cts2.Token);
                            _serverRequestedOn = DateTime.UtcNow;


                        }
                        catch (Exception ex)
                        {
                            _logger.Log(LogLevel.Error, "gamesession.gameserverFailure", "Failed to start game server", ex);

                        }
                    }
                    if (_server != null)
                    {
                        if (!state.IsServerPersistent())
                        {
                            
                            _ = _scene.RunTask(async ct =>
                            {
                                try
                                {
                                    await Task.Delay(1000 * 45, ct);
                                    if (_server != null && _serverPeer == null) //Server requested but it didn't connect to the game session in 60 seconds.
                                    {
                                        await using (var scope = _scene.CreateRequestScope())
                                        {
                                            var pools = scope.Resolve<ServerPoolProxy>();
                                            if (_server != null)
                                            {
                                                await pools.CloseServer(_server.GameServerId, CancellationToken.None);
                                            }

                                            _repository.RemoveGameSession(this);
                                            if (_gameCompleteCts != null)
                                            {
                                                _gameCompleteCts?.Cancel();
                                                _scene.Shutdown("gameserver.didnotconnect");
                                            }



                                        }
                                        return;
                                    }
                                    await Task.Delay(1000 * 15 * 5, ct);

                                    if (!_playerConnectedOnce && !ct.IsCancellationRequested)
                                    {
                                        await RequestShutdown("gamesession.empty");
                                    }
                                }
                                catch (OperationCanceledException) { }
                            });
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
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "gamesession.gameserverFailure", "Failed to start game server", ex);
                return false;
            }
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
                var ctx = new ClientLeavingContext(this, new PlayerPeer(peer.SessionId, new Player(peer.SessionId, userId)), HostSessionId == peer.SessionId);
                await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                {
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnClientLeaving(ctx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.OnClientLeaving event handlers", ex);
                    });
                }

                client.Peer = null;
                client.Status = PlayerStatus.Disconnected;

                BroadcastClientUpdate(client, userId, peer.SessionId);

                await EvaluateGameComplete();

                if (HostSessionId == peer.SessionId)
                {
                    await RequestShutdown("gamesession.gameServerLeft", TimeSpan.Zero, false);
                }
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

        public async Task Reset()
        {
            //Force completion of the game.
            await EvaluateGameComplete(true);

            foreach (var client in _clients.Values)
            {
                client.Reset();
            }

            _gameCompleteExecuted = false;

            await using (var scope = _scene.CreateRequestScope())
            {
                var ctx = new GameSessionResetContext(this, _scene);

                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.OnGameSessionReset(ctx), ex =>
                 {
                     _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.GameSessionCompleted event handlers", ex);
                 });
            }


        }

        public async Task<Action<Stream, ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer)
        {
            if (this._status != ServerStatus.Started)
            {
                throw new ClientException($"Unable to post result before game session start. Server status is {this._status}");
            }
            var session = await GetSessionAsync(remotePeer);
            if (session != null && session.User != null)
            {





                var memStream = new MemoryStream();
                inputStream.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);

                if (_clients.TryGetValue(session.User.Id, out var client))
                {
                    client.ResultData = memStream;

                    using var ctx = new PostingGameResultsCtx(this, _scene, remotePeer, session, memStream);
                    await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                    {
                        await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.PostingGameResults(ctx), ex =>
                        {
                            _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.PostingGameResults event handlers", ex);
                        });
                    }
                    memStream.Seek(0, SeekOrigin.Begin);

                    await EvaluateGameComplete();

                    var tcs = client.GameCompleteTcs;
                    if (tcs != null)
                    {
                        return await tcs.Task;
                    }

                }
                static void NoOp(Stream _, ISerializer _2) { };
                return NoOp;

            }
            else
            {
                throw new ClientException("unauthorized?reason=publicGame");
            }

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

        public DateTime OnCreated { get; } = DateTime.UtcNow;

        public void SetDimension(string dimension, string value)
        {
            lock (_syncRoot)
            {
                _dimensions[dimension] = value;
            }
        }

        private bool _gameCompleteExecuted = false;

        public event Action? OnGameSessionCompleted;

        private async Task EvaluateGameComplete(bool force = false)
        {
            Debug.Assert(_config != null);
            var ctx = new GameSessionCompleteCtx(this, _scene, _config, _clients.Select(kvp => new GameSessionResult(kvp.Key, kvp.Value.Peer, kvp.Value.Session, kvp.Value.ResultData ?? new MemoryStream())), _clients.Keys);


            async Task runHandlers()
            {
                await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                {
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.GameSessionCompleted(ctx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.GameSessionCompleted event handlers", ex);
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



            }

            bool shouldRunHandlers = false;
            var shouldCompleteGame = await ShouldCompleteGame();
            lock (this)
            {
                if (!_gameCompleteExecuted && (force || shouldCompleteGame))//All remaining clients sent their data
                {
                    _gameCompleteExecuted = true;

                    shouldRunHandlers = true;

                }
            }

            if (shouldRunHandlers)
            {
                _logger.Log(LogLevel.Info, "gameSession", "Completing game session", new { results = _clients.Select(kvp => new { client = kvp.Key, resultReceived = kvp.Value.ResultData != null }) });
                await runHandlers();
            }
        }

        private async Task<bool> ShouldCompleteGame()
        {
            var defaultValue = _clients.Values.All(c => c.ResultData != null || c.Peer == null);

            var ctx = new ShouldCompleteGameContext(_scene, this, defaultValue, _clients.Values);

            await using (var scope = _scene.CreateRequestScope())
            {
                await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.ShouldCompleteGame(ctx), ex =>
                {
                    _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.ShouldCompleteGame event handlers", ex);

                });
            }

            return ctx.ShouldComplete;
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
            _repository.RemoveGameSession(this);
            _gameCompleteCts?.Dispose();
            _gameCompleteCts = null;


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
                return new GameSessionConfigurationDto { Teams = _config.TeamsList, Parameters = _config.Parameters, UserIds = _config.UserIds, HostSessionId = SessionId.From(_config.HostSessionId), GameFinder = _config.GameFinder, PreferredRegions = _config.PreferredRegions };
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
                ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnCreatingReservation event", ex));


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
                   ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnReservationCancelled event", ex));

            }

        }

        private bool _reservationCleanupRunning = false;
        private async Task ReservationCleanupCallback(object? userState)
        {
            if (_gameCompleteCts == null)
            {
                return;
            }
            if (!_reservationCleanupRunning && !_gameCompleteCts.IsCancellationRequested)
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
                               ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnReservationCancelled event", ex));
                        }


                    }

                    if (!state.IsServerPersistent() && _playerConnectedOnce)
                    {
                        if (_server != null)
                        {
                            if (!_scene.RemotePeers.Any(p => p.SessionId != _server.GameServerSessionId) && !_reservationStates.Any(r => r.Value.ExpiresOn > DateTime.UtcNow))
                            {

                                await RequestShutdown("gamesession.empty");

                            }
                        }
                        else
                        {
                            if (!_scene.RemotePeers.Any() && !_reservationStates.Any(r => r.Value.ExpiresOn > DateTime.UtcNow))
                            {
                                await RequestShutdown("gamesession.empty");
                            }
                        }
                    }
                }
                finally
                {
                    _reservationCleanupRunning = false;
                }
            }


        }

        private DateTime _shuttingDownTime = DateTime.MaxValue;


        private bool _shutdown = false;
        private string? _shutdownReason;


        public bool ShouldShutdown([NotNullWhen(true)] out string? reason)
        {
            reason = _shutdownReason;

            return !_shutdown && _shuttingDownTime < DateTime.UtcNow;

        }

        private bool TryResetShutdown()
        {
            if (_shutdown)
            {
                return false;
            }
            else
            {
                _shuttingDownTime = DateTime.MaxValue;
                _shutdownReason = null;
                return true;
            }
        }
        private async Task RequestShutdown(string shutdownReason, TimeSpan keepAlive = default, bool runEvents = true)
        {
            await using var scope = _scene.CreateRequestScope();
            var ctx = new ShuttingDownContext(this, _scene, shutdownReason);
            ctx.KeepAlive = keepAlive;
            if (runEvents)
            {
                await scope.Resolve<IEnumerable<IGameSessionEventHandler>>().RunEventHandler(h => h.OnShuttingDown(ctx), ex => _logger.Log(LogLevel.Error, "gamesession", $"An error occurred while running {nameof(IGameSessionEventHandler.OnShuttingDown)}", ex));
            }
            _shutdownReason = ctx.ShutdownReason;
            _shuttingDownTime = DateTime.UtcNow + ctx.KeepAlive;
        }

        private async Task EvaluateShutdown()
        {
            if (ShouldShutdown(out var reason))
            {
                _repository.RemoveGameSession(this);
                await using var scope = _scene.CreateRequestScope();

                if (_server != null)
                {
                    var pools = scope.Resolve<ServerPoolProxy>();
                    await pools.CloseServer(_server.GameServerId, CancellationToken.None);
                    _server = null;
                }

                _gameCompleteCts?.Cancel();

                _scene.Shutdown(reason);

                _shutdown = true;
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

        private GameServer? _server;
        private DateTime _serverRequestedOn;

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

        public async Task<InspectLiveGameSessionResult> InspectAsync(CancellationToken cancellationToken)
        {
            var result = new InspectLiveGameSessionResult
            {
                Configuration = GetGameSessionConfig(),
                CreatedOnUtc = CreatedOn,
                Data = new JObject(),
                GameSessionId = GameSessionId,
                PlayersCount = _clients.Count(),
                HostSessionId = HostSessionId
            };

            await using var scope = _scene.CreateRequestScope();

            var handlers = scope.Resolve<IEnumerable<IGameSessionEventHandler>>();

            await handlers.RunEventHandler(h => h.OnInspectingGameSession(result), ex => _logger.Log(LogLevel.Error, "gamesession", $"An error occurred while running {nameof(IGameSessionEventHandler.OnInspectingGameSession)}", ex));
            return result;
        }
        #endregion

    }
}
