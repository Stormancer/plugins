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
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
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
        public string GameSessionId { get; internal set; } = default!;
    }

    class ServerPoolController : ControllerBase
    {
        private readonly ServerPools pools;
        private readonly IUserSessions sessions;

        public ServerPoolController(ServerPools pools, IUserSessions sessions)
        {
            this.pools = pools;
            this.sessions = sessions;
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            pools.SetShutdown(args.Peer.SessionId);
            return Task.CompletedTask;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<GameServerStartupParameters> SetReady(RequestContext<IScenePeerClient> ctx)
        {
            var session = await sessions.GetSession(ctx.RemotePeer,ctx.CancellationToken);
            if(session == null)
            {
                throw new ClientException("session.notFound");
            }
            if(session.platformId.Platform != DedicatedServerAuthProvider.PROVIDER_NAME)
            {
                throw new ClientException("serverPool.notAuthenticatedAsDedicatedServer");
            }

            return await pools.SetReady(session.platformId.PlatformUserId,ctx.RemotePeer);
        }
    }
}
