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

using MessagePack;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession.Models;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    [Service(Named = true, ServiceType = "stormancer.plugins.gamesession")]
    class GameSessionController : ControllerBase
    {
        private readonly IGameSessionService _service;
        private readonly IUserSessions _sessions;

        public GameSessionController(IGameSessionService service, IUserSessions sessions)
        {
            _service = service;
            _sessions = sessions;
        }

        protected override Task OnConnecting(IScenePeerClient client)
        {
            return _service.OnPeerConnecting(client);

        }

        protected override Task OnConnected(IScenePeerClient peer)
        {
            return _service.OnPeerConnected(peer);
        }

        protected override Task OnConnectionRejected(IScenePeerClient client)
        {
            return _service.OnPeerConnectionRejected(client);
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            return _service.OnPeerDisconnecting(args.Peer);
        }

        public async Task PostResults(RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer,ctx.CancellationToken);
            var writer = await _service.PostResults(ctx.InputStream, ctx.RemotePeer, session) ;
            if (!ctx.CancellationToken.IsCancellationRequested)
            {
                await ctx.SendValue(s =>
                {

                    writer(s, ctx.RemotePeer.Serializer());
                });
            }
        }

        public async Task Reset(RequestContext<IScenePeerClient> ctx)
        {
            if (_service.IsHost(ctx.RemotePeer.SessionId))
            {
                await _service.Reset();
            }
            else
            {
                throw new ClientException("forbidden");
            }
        }

        public async Task UpdateShutdownMode(RequestContext<IScenePeerClient> ctx)
        {
            ShutdownModeParameters shutdown = ctx.ReadObject<ShutdownModeParameters>();
            if (_service.IsHost(ctx.RemotePeer.SessionId))
            {
                await _service.UpdateShutdownMode(shutdown);
            }
            else
            {
                throw new ClientException("forbidden");
            }
        }

        public async Task GetGameSessionSettings(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            var config = _service.GetGameSessionConfig();
            if (string.IsNullOrEmpty(config.UserIds.FirstOrDefault(id => id == user?.Id)))
            {

                throw new ClientException($"unauthorized");
            }
        }


        [Api(ApiAccess.Public, ApiType.Rpc)]
        public System.Collections.Generic.IEnumerable<Team> GetTeams()
        {
            return _service.GetGameSessionConfig().Teams;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public void UpdateSettings(GameSessionSettingsRecord newSettings, RequestContext<IScenePeerClient> ctx)
        {
            if (_service.IsHost(ctx.RemotePeer.SessionId))
            {
                _service.UpdateSettings(newSettings);
            }
            else
            {
                throw new ClientException("forbidden");
            }
            
        }

        public void UpdateHostCandidates(IEnumerable<SessionId> sessionIds, RequestContext<IScenePeerClient> ctx)
        {
            if (_service.IsHost(ctx.RemotePeer.SessionId))
            {
                _service.UpdateHostCandidates(sessionIds);
            }
            else
            {
                throw new ClientException("forbidden");
            }
        }

        [S2SApi]
        public Task<GameSessionReservation?> CreateReservation(Team team, JObject args, CancellationToken cancellationToken)
        {

            return _service.CreateReservationAsync(team, args, cancellationToken);
        }

        [S2SApi]
        public Task CancelReservation(string id, CancellationToken cancellationToken)
        {
            return _service.CancelReservationAsync(id, cancellationToken);
        }

        [S2SApi]
        public Task<InspectLiveGameSessionResult> Inspect(CancellationToken cancellationToken)
        {
            return _service.InspectAsync(cancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.FireForget, Route = "player.ready")]
        public Task SetPlayerReady(string data, Packet<IScenePeerClient> packet)
        {

            return _service.SetPlayerReady(packet.Connection, data);
        }
        [Api(ApiAccess.Public, ApiType.FireForget, Route = "player.faulted")]
        public Task SetFaulted(Packet<IScenePeerClient> packet)
        {
            return _service.SetPeerFaulted(packet.Connection);
        }
    }

    /// <summary>
    /// A reservation in the game session.
    /// </summary>
    [MessagePackObject]
    public class GameSessionReservation
    {
        /// <summary>
        /// Gets or sets the date the reservation expires on.
        /// </summary>
        [Key(0)]
        public DateTime ExpiresOn { get; set; }

        /// <summary>
        /// Gets the id of the reservation.
        /// </summary>
        [Key(1)]
        public string ReservationId { get; set; } = default!;
    }

    /// <summary>
    /// Result of an inspect session request.
    /// </summary>
    [MessagePackObject]
    public class InspectLiveGameSessionResult
    {
        /// <summary>
        /// Gets the id of the game session.
        /// </summary>
        [Key(0)]
        public string GameSessionId { get; set; } = default!;

        /// <summary>
        /// Gets the date the game session was created.
        /// </summary>
        [Key(1)]
        public DateTime CreatedOnUtc { get; set; }


        /// <summary>
        /// Gets the current player count in the game session.
        /// </summary>
        [Key(2)]
        public int PlayersCount { get; set; }


        /// <summary>
        /// Gets the id of the host' session, if it exist.
        /// </summary>
        [Key(3)]
        public SessionId? HostSessionId { get; set; }

        /// <summary>
        /// Is the session a P2P session (no server)
        /// </summary>
        [Key(4)]
        public bool IsP2P { get; set; }

        /// <summary>
        /// Gets the id of the pool used to manage the game session server, if it has one.
        /// </summary>
        [Key(5)]
        public string? ServerPool { get; set; }

        /// <summary>
        /// Gets data about the game session.
        /// </summary>
        [Key(6)]
        public JObject Data { get; set; } = default!;

        /// <summary>
        /// Gets the configuration of the game session.
        /// </summary>
        [Key(7)]
        public GameSessionConfigurationDto? Configuration { get; set; }
    }
}
