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
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession.Models;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    [Service(Named = true, ServiceType = "stormancer.plugins.gamefinder")]
    class GameSessionController : ControllerBase
    {
        private readonly IGameSessionService _service;
        private readonly ILogger _logger;
        private readonly IUserSessions _sessions;
        private readonly IEnvironment _environment;

        public GameSessionController(IGameSessionService service, ILogger logger, IUserSessions sessions, IEnvironment environment)
        {
            _service = service;
            _logger = logger;
            _sessions = sessions;
            _environment = environment;
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
            var writer = await _service.PostResults(ctx.InputStream, ctx.RemotePeer);
            if (!ctx.CancellationToken.IsCancellationRequested)
            {
                await ctx.SendValue(s =>
                {
                    
                    writer(s, ctx.RemotePeer.Serializer());
                });
            }
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<HostInfosMessage> GetP2PToken(RequestContext<IScenePeerClient> ctx)
        {
            return await _service.CreateP2PToken(ctx.RemotePeer.SessionId);
        }

        public Task Reset(RequestContext<IScenePeerClient> ctx)
        {
            return _service.Reset();
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
            var user = await _sessions.GetUser(ctx.RemotePeer,ctx.CancellationToken);

            var config = _service.GetGameSessionConfig();
            if (string.IsNullOrEmpty(config.UserIds.FirstOrDefault(id => id == user?.Id)))
            {

                throw new ClientException($"unauthorized");
            }
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> GetGameSessionConnectionUrl(RequestContext<IScenePeerClient> ctx)
        {
            var id = _service.GetGameSessionConfig().HostUserId;

            if (!string.IsNullOrEmpty(id))
            {
                var session = await _sessions.GetSessionByUserId(id,ctx.CancellationToken);
                if (session != null)
                {
                    return "strm." + session.SessionId;
                }
            }

            throw new ClientException("no host configured in gameSession.");
        }

        [Api(ApiAccess.Public,ApiType.Rpc)]
        public System.Collections.Generic.IEnumerable<Team> GetTeams()
        {
            return _service.GetGameSessionConfig().Teams;
        }

        [S2SApi]
        public Task<GameSessionReservation?> CreateReservation(Team team, JObject args,CancellationToken cancellationToken)
        {
           
           return  _service.CreateReservationAsync(team, args,cancellationToken);
        }

        [S2SApi]
        public Task CancelReservation(string id, CancellationToken cancellationToken)
        {
            return _service.CancelReservationAsync(id,cancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.FireForget, Route ="player.ready")]
        public Task SetPlayerReady(string data, Packet<IScenePeerClient> packet)
        {
            
            return _service.SetPlayerReady(packet.Connection,data);
        }
        [Api(ApiAccess.Public, ApiType.FireForget, Route = "player.faulted")]
        public Task SetFaulted(Packet<IScenePeerClient> packet)
        {
            return _service.SetPeerFaulted(packet.Connection);
        }
    }

    public class GameSessionReservation
    {
        public DateTime ExpiresOn { get; set; }
        public string ReservationId { get; set; }
    }
}
