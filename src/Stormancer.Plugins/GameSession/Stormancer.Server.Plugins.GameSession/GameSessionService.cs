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

using Autofac.Core;
using MessagePack;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Components.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartFormat.Utilities;
using Stormancer.Abstractions.Server.Components;
using Stormancer.Core;
using Stormancer.Core.Helpers;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession.Models;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Represents a party in a game session.
    /// </summary>
    public class PartySummary
    {
        /// <summary>
        /// Gets the id of the party.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets the list of players in the party.
        /// </summary>
        public HashSet<SessionId> Players { get; } = new();
    }
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
    [MessagePackObject]
    public class HostInfosMessage
    {
        /// <summary>
        /// If P2P enabled, contains the connection token to the host.
        /// </summary>
        [Key(0)]
        public string? P2PToken { get; set; }

        /// <summary>
        /// True if the receiving peer is the host.
        /// </summary>
        [Key(1)]
        public bool IsHost { get; set; }

        /// <summary>
        /// Session id of the host.
        /// </summary>
        [Key(2)]
        public SessionId HostSessionId { get; set; }

        /// <summary>
        /// Arguments necessary to start the game session.
        /// </summary>
        /// <remarks>
        /// Built from context by 
        /// </remarks>
        [Key(3)]
        public Dictionary<string, string> Arguments { get; set; }
    }

    /// <summary>
    /// A client in the game session.
    /// </summary>
    public class Client
    {
        internal Client(IScenePeerClient peer, SessionId sessionId, Session session, string partyId)
        {
            Peer = peer;
            SessionId = sessionId;
            Reset();
            Status = PlayerStatus.NotConnected;
            Session = session;
            PartyId = partyId;
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
        public string PartyId { get; }

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
        private readonly JsonSerializer _jsonSerializer;
        private readonly IEnumerable<IHostSelectionPolicy> _hostSelectionPolicies;
        private TimeSpan _gameSessionTimeout = TimeSpan.MaxValue;
        private GameSessionConfiguration? _config;
        private readonly CancellationTokenSource _sceneCts = new();


        private readonly ConcurrentDictionary<string, Client> _clients = new();
        private readonly Dictionary<SessionId, Session> _connectedSessions = new();

        private ServerStatus _status = ServerStatus.WaitingPlayers;
        // A source that is canceled when the game session is complete
        private CancellationTokenSource? _gameCompleteCts = new();

        //set to true to indicate a player connected to the session at least once.
        private bool _playerConnectedOnce = false;

        private readonly object _lock = new();
        private TaskCompletionSource<IScenePeerClient>? _hostFoundTcs = null;
        private ShutdownMode _shutdownMode;
        private DateTime _shutdownDate;

        private DateTime _lastServerKeepAlive;

        public GameSessionService(
            GameSessionState state,
            GameSessionAnalyticsWorker analyticsWorker,
            ISceneHost scene,
            IConfiguration configuration,
            IEnvironment environment,
            ILogger logger,
            RpcService rpc,
            GameSessionsRepository repository,
            ISerializer serializer,
            GameSessionEventsRepository events,
            JsonSerializer jsonSerializer,
            IEnumerable<IHostSelectionPolicy> hostSelectionPolicies
            )
        {
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
            _jsonSerializer = jsonSerializer;
            _hostSelectionPolicies = hostSelectionPolicies;
            ApplySettings();

            events.PostEvent(new GameSessionEvent() { GameSessionId = scene.Id, Type = "gamesessionCreated" });
            analyticsWorker.AddGameSession(this);
            scene.Shuttingdown.Add(async args =>
            {
                try
                {
                    events.PostEvent(new GameSessionEvent() { GameSessionId = scene.Id, Type = "gamesessionShutdown" });
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
                }
                finally
                {
                    await CloseGameServer();
                }

            });
            scene.AuthorizeP2P.Add(async args =>
            {
                args.Accept = true;
                await using var scope = _scene.CreateRequestScope();
                var handlers = scope.ResolveAll<IP2pEventHandler>();

                var ctx = new OnGetP2PMetadataContext(new Dictionary<string, string>(), _scene, args.Origin, args.Target);
                await handlers.RunEventHandler(async h => await h.OnGetP2PMetadata(ctx), ex =>
                {
                    _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running IP2pEventHandler.OnGetP2PMetadata event handlers", ex);
                });

                foreach (var (key, value) in ctx.Metadata)
                {
                    args.SetMetadata(key, value);
                }
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
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running the game session cleanup method.", ex);
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

                //_logger.Log(LogLevel.Info, _scene.Id, "Set player ready", new { peer.SessionId, session = session != null });

                //Peer not authd.
                if (session is null)
                {
                    return;
                }

                if (session.IsDedicatedServer())
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
                }


                if (IsHost(peer.SessionId))
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

        public void SetConfiguration(Dictionary<string, object?> metadata)
        {
            if (metadata.ContainsKey("gameSession"))
            {
                var obj = metadata["gameSession"];
                var str = JObject.FromObject(obj!, _jsonSerializer).ToString();

                _config = JsonConvert.DeserializeObject<GameSessionConfiguration>(str, _jsonSerializer.Converters.ToArray()) ?? new GameSessionConfiguration();

                UpdateSettings(_config.Settings, _config.TeamsConfiguration);
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

            if (session.IsDedicatedServer())
            {
                _isDedicatedServer = true;
                return;
            }

            lock (syncRoot)
            {


            }
            string partyId = string.Empty;
            if (peer.ContentType == "stormancer/partyId")
            {
                partyId = System.Text.Encoding.ASCII.GetString(peer.UserData);
            }
            var client = new Client(peer, peer.SessionId, session, partyId);
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
            //_logger.Log(LogLevel.Info, _scene.Id, "Signal host ready", new { peer.SessionId, userId, server = _server != null });

            if (_server == null)
            {
                await TryStart();
            }

            var sessionId = peer.SessionId;

            _status = ServerStatus.Started;
            //await SendP2PToken(Enumerable.Repeat(sessionId, 1), true, "", default);
            if (state.DirectConnectionEnabled())
            {
                UpdateTopology(sessionId, GameSessionHostState.Ready);
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

            var client = _clients.FirstOrDefault(client => client.Value.SessionId == peer.SessionId);

            var session = client.Value?.Session ?? await sessions.GetSession(peer, CancellationToken.None);


            if (session is null)
            {
                return;
            }
            _connectedSessions.Add(peer.SessionId, session);

            SendSnapshot(session.SessionId);

            var isDedicatedServer = session.IsDedicatedServer();
            //Is authenticated as a dedicated server


            if (isDedicatedServer)
            {
                SelectHost();
            }
            else
            {
                _playerConnectedOnce = true;
                var userId = client.Key;


                var (reservationId, reservation) = _reservationStates.FirstOrDefault(r => r.Value.UserIds.Contains(userId));
                if (reservation != null)
                {
                    reservation.UserIds.Remove(userId);
                    if (reservation.UserIds.Count == 0)
                    {
                        _reservationStates.TryRemove(reservationId, out _);
                    }
                }



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

                var serverFound = await TryStart();

                _analytics.PlayerJoined(userId, peer.SessionId.ToString(), _scene.Id);

                //Check if the gameSession is Dedicated or listen-server            
                // If the host is not defined a P2P was sent with "" to notify client is host.
                if (state.DirectConnectionEnabled() && state.GameServerPool() == null)
                {
                    SelectHost();

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
        }

        private void SelectHost()
        {
            List<Session> candidates = new();
            foreach (var (sessionId, session) in _connectedSessions)
            {
                foreach (var provider in _hostSelectionPolicies)
                {
                    if (provider.TryConfigure(_config?.HostSelectionConfiguration, this))
                    {
                        if (provider.IsHostCandidate(session))
                        {
                            candidates.Add(session);
                        }
                    }

                }
            }
            if (candidates.Count == 0 && _currentTopology != null && !_currentTopology.Host.IsEmpty())
            {
                UpdateTopology(_currentTopology.Host, GameSessionHostState.Disconnected);
            }
            if (candidates.Count > 0)
            {
                var host = candidates.First();
                UpdateTopology(host.SessionId, GameSessionHostState.Connected);
            }


        }

        public SessionId HostSessionId => _currentTopology?.Host ?? SessionId.Empty;

        private Task<bool>? _serverStartTask = null;

        public Task<GameServer?> WaitServerStartAsync(CancellationToken cancellationToken)
        {
            async Task<GameServer?> TrySartImpl()
            {
                await TryStart();
                return _server;
            }
            return TrySartImpl().WaitAsync(cancellationToken);
        }

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
                var settings = _currentGameSessionSettings.Settings;
                var teams = _currentGameSessionSettings.Teams;
                var ctx = new GameSessionStartingContext(this, this._scene, _config, settings, teams);

                await using (var scope = _scene.DependencyResolver.CreateChild(API.Constants.ApiRequestTag))
                {
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(h => h.GameSessionStarting(ctx), ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing GameSessionStarting event", ex));
                }


                UpdateSettings(settings, teams);

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
                            _server = await pools.TryStartGameServer(poolId, GameSessionId, _scene.Template, _config, this._currentGameSessionSettings?.Settings ?? new Dictionary<string, string>(), cts2.Token);
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
                                    await Task.Delay(1000 * 60, ct);
                                    if (HostSessionId.IsEmpty()) //Server requested but it didn't connect to the game session in 60 seconds.
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

        //private TaskCompletionSource<IScenePeerClient> GetHostTcs()
        //{
        //    lock (_lock)
        //    {
        //        if (_serverPeer == null)
        //        {
        //            _serverPeer = new TaskCompletionSource<IScenePeerClient>();
        //        }
        //    }
        //    return _serverPeer;
        //}

        public async Task OnPeerDisconnecting(IScenePeerClient peer)
        {
            Debug.Assert(_config != null);

            _connectedSessions.Remove(peer.SessionId);

            SelectHost();


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

                await EvaluateGameComplete();

                //if (HostSessionId == peer.SessionId)
                //{
                //    await RequestShutdown("gamesession.gameServerLeft", TimeSpan.Zero, false);
                //}
            }

            if (ShouldClose())
            {
                foreach (var c in _clients.Values)
                {
                    if (c.Peer != null)
                    {
                        await c.Peer.Disconnect("gamesession.closed");
                    }
                }
                _scene.Shutdown("gamesession.closed");

            }


        }
        private bool ShouldClose()
        {
            return _isDedicatedServer && _scene.RemotePeers.Count() == 1;

        }

        private async ValueTask CloseGameServer()
        {
            if (!HostSessionId.IsEmpty())
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

        public async Task<Action<Stream, ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer, Session session)
        {
            if (this._status != ServerStatus.Started)
            {
                throw new ClientException($"Unable to post result before game session start. Server status is {this._status}");
            }
            _logger.Log(LogLevel.Info, $"gamesession.{GameSessionId}", $"Running Post result from {session.User?.Id}", new { }, GameSessionId, session.User?.Id);

            if (session != null && session.User != null)
            {

                var memStream = new MemoryStream();
                inputStream.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);


                _logger.Log(LogLevel.Info, $"gamesession.{GameSessionId}", $"Running Posting result from {session.User?.Id}", new { }, GameSessionId, session.User?.Id);
                using var ctx = new PostingGameResultsCtx(this, _scene, remotePeer, session, memStream);
                await using (var scope = _scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
                {
                    _logger.Log(LogLevel.Info, $"gamesession.{GameSessionId}", $"Running Posting result handlers from {session.User?.Id}", new { }, GameSessionId, session.User?.Id);
                    await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(eh => eh.PostingGameResults(ctx), ex =>
                    {
                        _logger.Log(LogLevel.Error, "gameSession", "An error occurred while running gameSession.PostingGameResults event handlers", ex);
                    });
                    _logger.Log(LogLevel.Info, $"gamesession.{GameSessionId}", $"Ran Posting result handlers from {session.User?.Id}", new { }, GameSessionId, session.User?.Id);
                }
                memStream.Seek(0, SeekOrigin.Begin);


                if (_clients.TryGetValue(session.User.Id, out var client))
                {
                    client.ResultData = memStream;


                    await EvaluateGameComplete();

                    var tcs = client.GameCompleteTcs;
                    if (tcs != null)
                    {
                        return await tcs.Task;
                    }

                }
                static void NoOp(Stream _, ISerializer _2) { }
                ;
                return NoOp;

            }
            else
            {
                throw new ClientException("unauthorized?reason=notAuthenticated");
            }

        }






        public string GameSessionId => _scene.Id;

        public DateTime CreatedOn { get; } = DateTime.UtcNow;
        public int PlayerCount => _clients.Count + _reservationStates.Select(r => r.Value.UserIds).Count();

        private object _syncRoot = new object();
        private Dictionary<string, string> _dimensions = new Dictionary<string, string>();
        private FrozenDictionary<string, string>? _frozenDimensions;
        public FrozenDictionary<string, string> Dimensions
        {
            get
            {
                lock (_syncRoot)
                {
                    if (_frozenDimensions == null)
                    {
                        _frozenDimensions = _dimensions.ToFrozenDictionary();
                    }
                    return _frozenDimensions;
                }
            }
        }

        public DateTime OnCreated { get; } = DateTime.UtcNow;

        public ISceneHost Scene => _scene;

        public void SetDimension(string dimension, string value)
        {
            lock (_syncRoot)
            {
                _dimensions[dimension] = value;
                _frozenDimensions = null;
            }
        }

        private bool _gameCompleteExecuted = false;

        public event Action? OnGameSessionCompleted;

        private async Task EvaluateGameComplete(bool force = false)
        {
            if (_config == null)
            {
                return;
            }
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
                //_logger.Log(LogLevel.Info, "gameSession", "Completing game session", new { results = _clients.Select(kvp => new { client = kvp.Key, resultReceived = kvp.Value.ResultData != null }) });
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
            return HostSessionId == sessionId;
        }

        public async ValueTask DisposeAsync()
        {
            _repository.RemoveGameSession(this);
            _gameCompleteCts?.Dispose();
            _gameCompleteCts = null;



        }

        public GameSessionConfigurationDto? GetGameSessionConfig()
        {
            if (_config == null)
            {
                return null;
            }
            else
            {
                return new GameSessionConfigurationDto { Teams = _config.TeamsList, Parameters = _config.Parameters, UserIds = _config.UserIds, HostSessionId = HostSessionId, GameFinder = _config.GameFinder, PreferredRegions = _config.PreferredRegions };
            }

        }



        public void OnConfigurationChanged()
        {
            ApplySettings();
        }

        public void UpdateGameSessionConfig(Action<GameSessionConfiguration> gameSessionConfigUpdater)
        {
            if (_config is null)
            {
                throw new InvalidOperationException("_config not initialized.");
            }
            if (gameSessionConfigUpdater is null)
            {
                throw new ArgumentNullException(nameof(gameSessionConfigUpdater));
            }

            gameSessionConfigUpdater(_config);
            UpdateSettings(_config.Settings, _config.TeamsConfiguration);
        }

        private object syncRoot = new object();

        public void UpdateSettings(GameSessionSettingsRecord record)
        {
            UpdateSettings(record.Settings, record.Teams);
        }

        public void UpdateHostCandidates(IEnumerable<SessionId> sessionIds)
        {
            _hostCandidates.Clear();
            foreach (var sessionId in sessionIds)
            {
                _hostCandidates.Add(sessionId);
            }

            SelectHost();
        }

        public IEnumerable<SessionId> GetHostCandidates()
        {
            return _hostCandidates;
        }

        private HashSet<SessionId> _hostCandidates;

        #region Reservations
        public IEnumerable<PartySummary> GetParties(bool includeReservations)
        {
            var result = new Dictionary<string, PartySummary>();

            foreach (var (userId, client) in _clients)
            {

                if (!result.TryGetValue(client.PartyId, out var party))
                {
                    party = new PartySummary { Id = client.PartyId };
                    result.Add(client.PartyId, party);
                }
                party.Players.Add(client.SessionId);
            }
            if (includeReservations)
            {

                foreach (var (id, reservation) in _reservationStates)
                {
                    foreach (var pa in reservation.Parties)
                    {
                        foreach (var (_, player) in pa.Players)
                        {
                            if (!result.TryGetValue(pa.PartyId, out var party))
                            {
                                party = new PartySummary { Id = pa.PartyId };
                                result.Add(pa.PartyId, party);
                            }
                            party.Players.Add(player.SessionId);
                        }
                    }
                }
            }
            return result.Values;
        }

        public IEnumerable<TeamConfigurationRecord> GetTeamsConfiguration()
        {
            return _currentGameSessionSettings?.Teams ?? Enumerable.Empty<TeamConfigurationRecord>();
        }

        private static Dictionary<string, string> _emptySettings = [];
        public IReadOnlyDictionary<string, string> GetSettings()
        {
            return _currentGameSessionSettings?.Settings ?? _emptySettings;
        }

        /// <summary>
        /// Can a proposed party size fit into the game session.
        /// </summary>
        /// <param name="partySize">The party size we are trying to fit.</param>
        /// <param name="acceptSplit">Do we accept to split the parties between several teams</param>
        /// <returns></returns>
        public bool CanFit(int partySize, bool acceptSplit = false)
        {
            var parties = GetParties(true);

            //Game session settings not yet initialized.
            if (_currentGameSessionSettings == null)
            {
                return false;
            }

            var teamsConfig = _currentGameSessionSettings.Teams;

            //Unlimited
            if (teamsConfig.Count == 0)
            {
                return true;
            }

            //If we accept splitting, it's simple, we just check that the party will fit in the max player count of the game.
            if (acceptSplit)
            {
                var currentCount = parties.Sum(p => p.Players.Count);
                var max = teamsConfig.Sum(t => t.Slots);
                return currentCount + partySize <= max;
            }

            //Check that the party isn't too big for any team.
            if (teamsConfig.Max(t => t.AvailableSlots) < partySize)
            {
                return false;
            }

            //TODO: we should remove players in fixed slots and then check if we can fit the others.
            return true;


        }

        public async Task<GameSessionReservation?> CreateReservationAsync(Team team, JObject args, CancellationToken cancellationToken)
        {
            //TODO refactor the team system...
            if (_config == null)
            {
                return null;
            }

            var pendingPlayers = new HashSet<string>(team.AllPlayers.Select(p => p.UserId));
            foreach (var id in _clients.Values.Select(t => t.Session?.User?.Id ?? string.Empty))
            {
                if (pendingPlayers.Contains(id))
                {
                    pendingPlayers.Remove(id);
                }
            }
            foreach (var id in _reservationStates.Values.SelectMany(r => r.UserIds))
            {
                if (pendingPlayers.Contains(id))
                {
                    pendingPlayers.Remove(id);
                }
            }

            if (!pendingPlayers.Any())
            {
                return new GameSessionReservation();
            }

            await using var scope = _scene.CreateRequestScope();

            if (!CanFit(pendingPlayers.Count, true))
            {
                return null;
            }


            var reservationState = new ReservationState();
            var ctx = new CreatingReservationContext(this, _scene, _config, team, args, reservationState.ReservationId);

            await scope.ResolveAll<IGameSessionEventHandler>().RunEventHandler(
                h => h.OnCreatingReservation(ctx),
                ex => _logger.Log(LogLevel.Error, "gameSession", "An error occurred while executing OnCreatingReservation event", ex));


            if (ctx.Accept)
            {

                lock (syncRoot)
                {
                    reservationState.UserIds.AddRange(pendingPlayers);
                    reservationState.Parties = team.Parties;

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
            if (_shutdownReason == null)
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
            public List<Party> Parties { get; internal set; }
        }

        private ConcurrentDictionary<Guid, ReservationState> _reservationStates = new ConcurrentDictionary<Guid, ReservationState>();

        private GameServer? _server;
        private DateTime _serverRequestedOn;
        private bool _isDedicatedServer;

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

        private int _currentVersion = 0;
        private GameSessionSettingsRecord? _currentGameSessionSettings;
        private TopologyUpdateRecord? _currentTopology;

        private object _stateSyncRoot = new object();

        private void UpdateTopology(SessionId host, GameSessionHostState state)
        {
            lock (_stateSyncRoot)
            {

                var record = new TopologyUpdateRecord { Host = host, State = state };
                if (!record.Equals(_currentTopology))
                {
                    _currentTopology = record;
                    _currentVersion++;
                    SendRecord(GameSessionRecordType.TopologyUpdated, record, _currentVersion);
                }
            }
        }
        private void SetTopologyError(string error)
        {
            lock (_stateSyncRoot)
            {
                var record = new TopologyUpdateRecord { Error = error };

                if (!record.Equals(_currentTopology))
                {
                    _currentTopology = record;
                    _currentVersion++;
                    SendRecord(GameSessionRecordType.TopologyUpdated, record, _currentVersion);
                }
            }
        }
        private void UpdateSettings(Dictionary<string, string> Settings, List<TeamConfigurationRecord> teamConfigurations)
        {
            lock (_stateSyncRoot)
            {
                var record = new GameSessionSettingsRecord { Settings = Settings, Teams = teamConfigurations };
                _currentGameSessionSettings = record;
                _currentVersion++;
                SendRecord(GameSessionRecordType.Settings, record, _currentVersion);
            }
        }


        private void SendSnapshot(SessionId target)
        {
            lock (_stateSyncRoot)
            {
                var header = new GameSessionRecordHeader { Type = GameSessionRecordType.Snapshot, Version = _currentVersion };

                _scene.Send(new MatchPeerFilter(target), "gamesession.records", static (IBufferWriter<byte> writer, (ISerializer, GameSessionSettingsRecord?, TopologyUpdateRecord?, GameSessionRecordHeader) t) =>
                {
                    var (serializer, settings, topology, header) = t;
                    serializer.Serialize(header, writer);
                    serializer.Serialize(new GameSessionSnapshot { SettingsSet = settings != null, TopologySet = topology != null }, writer);

                    if (settings != null)
                    {
                        serializer.Serialize(settings, writer);
                    }

                    if (topology != null)
                    {
                        serializer.Serialize(topology, writer);
                    }

                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (_serializer, _currentGameSessionSettings, _currentTopology, header));
            }
        }

        private void SendRecord<T>(GameSessionRecordType type, T record, int version)
        {
            var header = new GameSessionRecordHeader { Type = type, Version = version };
            _scene.Send(new MatchAllFilter(), "gamesession.records", static (IBufferWriter<byte> writer, (ISerializer, T, GameSessionRecordHeader) t) =>
            {
                var (serializer, record, header) = t;
                serializer.Serialize(header, writer);
                serializer.Serialize(record, writer);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (_serializer, record, header));
        }


    }
    public enum GameSessionRecordType
    {
        TopologyUpdated,
        Settings,
        Snapshot,
    }

    [MessagePackObject]
    public class GameSessionRecordHeader
    {
        [Key(0)]
        public int Version { get; set; }
        [Key(1)]
        public GameSessionRecordType Type { get; set; }


    }

    [MessagePackObject]
    public class GameSessionSnapshot
    {
        [Key(0)]
        public bool TopologySet { get; set; }

        [Key(1)]
        public bool SettingsSet { get; set; }

    }

    /// <summary>
    /// Team configuration in a game session.
    /// </summary>
    [MessagePackObject]
    public class TeamConfigurationRecord
    {
        /// <summary>
        /// Total slots in the team.
        /// </summary>
        [Key(0)]
        public int Slots { get; set; }

        /// <summary>
        /// Players declared in the team.
        /// </summary>
        [Key(1)]
        public List<SessionId> FixedSlots { get; set; } = new List<SessionId>();


        /// <summary>
        /// Currently available slots in the team.
        /// </summary>
        [IgnoreMember]
        public int AvailableSlots => Slots - FixedSlots.Count();
    }

    [MessagePackObject]
    public class GameSessionSettingsRecord
    {
        [Key(0)]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        [Key(1)]
        public List<TeamConfigurationRecord> Teams { get; set; } = new List<TeamConfigurationRecord>();
    }

    public enum GameSessionHostState
    {
        Disconnected,
        Connected,
        Ready,

    }
    [MessagePackObject]
    public class TopologyUpdateRecord : IEquatable<TopologyUpdateRecord>
    {

        [Key(0)]
        public string Error { get; set; } = string.Empty;

        [Key(1)]
        public SessionId Host { get; init; }

        [Key(2)]
        public GameSessionHostState State { get; init; }

        public bool Equals(TopologyUpdateRecord? other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Host == other.Host && this.State == other.State;
        }
    }
}
