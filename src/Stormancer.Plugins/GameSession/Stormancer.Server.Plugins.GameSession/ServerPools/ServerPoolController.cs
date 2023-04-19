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

using MsgPack.Serialization;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    /// <summary>
    /// Startup data passed to a game server.
    /// </summary>
    public class GameServerStartupParameters
    {
        /// <summary>
        /// Gets the connection to the game session.
        /// </summary>
        [MessagePackMember(0)]
        public string GameSessionConnectionToken { get; set; } = default!;

        /// <summary>
        /// Gets or sets the gamesession config.
        /// </summary>
        [MessagePackMember(1)]
        public GameSessionConfiguration Config { get; set; } = default!;

        /// <summary>
        /// Gets or sets the game session Id.
        /// </summary>
        [MessagePackMember(2)]
        public string GameSessionId { get; set; } = default!;
    }

    [Service(Named = false, ServiceType = "stormancer.plugins.serverPool")]
    class ServerPoolController : ControllerBase
    {
        private readonly ServerPools pools;
        private readonly IUserSessions sessions;
        private readonly IGameSessions gamesessions;
        private readonly AgentBasedGameServerProvider _agentsRepository;

        public ServerPoolController(ServerPools pools, IUserSessions sessions, IGameSessions gamesessions, AgentBasedGameServerProvider agentsRepository)
        {
            this.pools = pools;
            this.sessions = sessions;
            this.gamesessions = gamesessions;
            _agentsRepository = agentsRepository;
        }

        private static ConcurrentDictionary<SessionId, Session> _sessions = new ConcurrentDictionary<SessionId, Session>();
        protected override async Task OnDisconnected(DisconnectedArgs args)
        {
            //var session = await sessions.GetSessionById(args.Peer.SessionId, CancellationToken.None);
            if (_sessions.TryRemove(args.Peer.SessionId, out var session))
            {

                if (session.platformId.Platform == GameServerAgentConstants.TYPE) //AGENT
                {
                    _agentsRepository.AgentDisconnected(args.Peer, session);
                }
                else
                {
                    pools.RemoveGameServer(session.platformId.PlatformUserId); //GAMESERVER
                }
            }

        }

        protected override async Task OnConnected(IScenePeerClient peer)
        {
            var session = await sessions.GetSession(peer, CancellationToken.None);

            if (session != null)
            {
                _sessions[session.SessionId] = session;
                if (session.platformId.Platform == GameServerAgentConstants.TYPE)
                {
                    _agentsRepository.AgentConnected(peer, session);
                }
            }

        }

        /// <summary>
        /// Method called by the game server to wait for a gamesession to be ready.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        /// <exception cref="ClientException"></exception>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<GameServerStartupParameters> WaitGameSession(RequestContext<IScenePeerClient> ctx)
        {
            var session = await sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null)
            {
                throw new ClientException("session.notFound");
            }



            var parameters = await pools.WaitGameAvailableAsync(session, ctx.RemotePeer, ctx.CancellationToken);
            if (parameters != null)
            {
                parameters.GameSessionConnectionToken = await gamesessions.CreateConnectionToken(parameters.GameSessionId, session.SessionId);
            }
            if (parameters == null)
            {
                throw new ClientException("serverPool.notAuthorized");
            }
            return parameters;
        }

        [S2SApi]
        public Task<GameServer> WaitGameServer(string poolId, string gameSessionId, GameSessionConfiguration config, CancellationToken cancellationToken)
        {
            return pools.WaitGameServer(poolId, gameSessionId, config, cancellationToken);

        }

        [S2SApi]
        public Task CloseServer(GameServerId id)
        {
            return pools.CloseServer(id);
        }
    }
}
