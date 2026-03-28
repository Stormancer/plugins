using MessagePack;
using Stormancer.Cluster;
using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{

    /// <summary>
    /// Extension methods for host based topology.
    /// </summary>
    public static class HostClientsTopologyExtensions
    {
        /// <summary>
        /// Adds host client topology management to the scene. The scene must be a gamesession.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static ISceneHost AddHostClientsTopology(this ISceneHost scene)
        {
            scene.TemplateMetadata[GameSessionPlugin.TOPOLOGY_HOST_METADATA_KEY] = "1.0.0";
            return scene;
        }
    }

    /// <summary>
    /// Stores the state of the host elector.
    /// </summary>
    public class HostClientsTopologyState
    {

        internal HostClientsTopologyState(ISceneHost scene)
        {
            Configuration = GameSessionsExtensions.GetConfig(scene.Template);
        }
        internal bool Initialized = false;
        internal bool SupportsHostMigration = false;
        internal Func<Session, bool> IsHostCandidate = (_) => true;
        internal object SyncRoot = new();

        /// <summary>
        /// Gets the currently selected host.
        /// </summary>
        public SessionId CurrentHostSessionId { get; set; }

        /// <summary>
        /// Candidates for leadership
        /// </summary>
        public HashSet<SessionId> Candidates { get; } = new();


        internal int nextMessageId = 0;


        /// <summary>
        /// Gets the configuration of the gamesession.
        /// </summary>
        public GameSessionTemplateConfiguration Configuration { get; }

        internal Dictionary<SessionId, Session> ConnectedPeers { get; } = new();
    }


    /// <summary>
    /// Type of message sent to advertise a topology change to clients.
    /// </summary>
    [MessagePackObject]
    public class HostClientsTopologyUpdateMessage
    {
        /// <summary>
        /// Gets or sets the new host
        /// </summary>
        /// <remarks>
        /// A default <see cref="SessionId"/> object means that there is no host available anymore.
        /// </remarks>
        [Key(0)]
        public required SessionId NewHost { get; init; }


        /// <summary>
        /// Gets or sets a an error string.
        /// </summary>
        /// <remarks>
        /// If error is set, the topology won't be updated anymore, the peers can abandon.
        /// </remarks>
        [Key(1)]
        public string? Error { get; init; }

        /// <summary>
        /// Message id.
        /// </summary>
        [Key(2)]
        public required int Id { get; init; }
    }

    /// <summary>
    /// Controller class providing APIs for maintaining an host in a game session.
    /// </summary>
    public class HostClientsTopologyController : ControllerBase
    {
        private readonly IGameSessionService _service;
        private readonly IUserSessions _userSessions;
        private readonly HostClientsTopologyState _state;
        private readonly ISerializer _serializer;
        private readonly ISceneHost _scene;

        /// <summary>
        /// Creates an instance of <see cref="HostClientsTopologyController"/>
        /// </summary>
        public HostClientsTopologyController(
            IGameSessionService service,
            IUserSessions userSessions,
            HostClientsTopologyState state,
            ISerializer serializer,
            ISceneHost scene)
        {
            _service = service;
            _userSessions = userSessions;
            _state = state;
            _serializer = serializer;
            _scene = scene;
        }

        private void Initialize()
        {
            if (!_state.Initialized)
            {
                var hostSessionId = GetHost();
                if (!hostSessionId.IsEmpty())
                {
                    _state.IsHostCandidate = (s) => s.SessionId == hostSessionId;
                }
                else if (_state.Configuration.GameServerConfig.useGameServerGetter(_scene))
                {
                    _state.IsHostCandidate = _service.IsDedicatedServer;
                    _ = WaitServerAndElect();
                }
                _state.Initialized = true;
            }
        }

        private async Task WaitServerAndElect()
        {
            ServerPool.GameServer? server;
            try
            {
                server = await _service.WaitServerStartAsync(default);

            }
            catch (Exception)
            {
                server = null;
            }

            // If no server found, we instead  switch to client host as a fallback.
            if (server == null)
            {


                lock (_state.SyncRoot)
                {
                    _state.IsHostCandidate = (_) => true;
                    _state.Candidates.Clear();

                    foreach (var (sessionId, session) in _state.ConnectedPeers)
                    {


                        if (_state.IsHostCandidate(session))
                        {
                            _state.Candidates.Add(sessionId);
                        }
                    }
                    TryElectHost();
                }
            }
        }

        /// <summary>
        /// Called by peers to notify the server that they are ready to host the session.
        /// </summary>
        /// <remarks>
        /// Peers that must not be hosts should not call this API.
        /// </remarks>
        /// <param name="peer"></param>
        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task NotifyHostCandidate(IScenePeerClient peer)
        {
            var session = await _userSessions.GetSession(peer, default);
            if (session == null)
            {
                await peer.Disconnect("notAuthenticated");
                return;
            }

            lock (_state.SyncRoot)
            {
                _state.ConnectedPeers.Add(session.SessionId, session);
                if (_state.IsHostCandidate(session))
                {
                    _state.Candidates.Add(peer.SessionId);
                }
                if (TryElectHost())
                {
                    BroadcastHostUpdate();
                }
            }

        }

        private SessionId GetHost()
        {
            return _service.GetGameSessionConfig()?.HostSessionId ?? SessionId.Empty;
        }
        ///<inheritdoc/>
        protected override Task OnConnected(IScenePeerClient peer)
        {
            lock (_state.SyncRoot)
            {
                Initialize();
            }
            return Task.CompletedTask;
        }

        private void BroadcastHostUpdate()
        {
            var msg = new HostClientsTopologyUpdateMessage { Id = _state.nextMessageId++, NewHost = _state.CurrentHostSessionId };
            _scene.Send(_scene.MatchAllFilter, "hostclientTopology.update", (writer, tuple) =>
            {
                var (msg, serializer) = tuple;

                serializer.Serialize(msg, writer);

            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, (msg, _serializer));
        }

        private bool TryElectHost()
        {
            if (!_state.CurrentHostSessionId.IsEmpty())
            {
                return false;
            }
            else
            {
                foreach (var peer in this._scene.RemotePeers)
                {
                    if (_state.Candidates.Contains(peer.SessionId))
                    {
                        _state.CurrentHostSessionId = peer.SessionId;
                        return true;
                    }
                }

                return false;

            }
        }

        ///<inheritdoc/>
        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            lock (_state.SyncRoot)
            {
                _state.ConnectedPeers.Remove(args.Peer.SessionId);
                if (_state.Candidates.Remove(args.Peer.SessionId))
                {

                    if (_state.CurrentHostSessionId == args.Peer.SessionId)
                    {
                        _state.CurrentHostSessionId = SessionId.Empty;

                        TryElectHost();

                        //Always broadcast host update if the current host disconnected.
                        BroadcastHostUpdate();
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
