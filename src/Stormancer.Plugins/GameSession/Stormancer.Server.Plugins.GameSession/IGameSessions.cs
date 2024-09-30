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
using Stormancer.Abstractions.Server.Components;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Connection token formats supported by <see cref="IGameSessions"/>.
    /// </summary>
    public enum TokenVersion
    {
        /// <summary>
        /// A V1 connection token (deprecated)
        /// </summary>
        V1,

        /// <summary>
        /// A V3 connection token.
        /// </summary>
        V3
    }

    /// <summary>
    /// Provides functions to create game sessions and connections tokens to game sessions.
    /// </summary>
    public interface IGameSessions
    {
        /// <summary>
        /// Creates a game session.
        /// </summary>
        /// <param name="template"></param>
        /// <param name="id"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        Task Create(string template, string id, GameSessionConfiguration config);

        /// <summary>
        /// Create a connection token for a given user and game session scene.
        /// </summary>
        /// <param name="id">Id of the game session's scene</param>
        /// <param name="userSessionId">Session Id of the target user</param>
        /// <param name="version">Version of the resulting token payload</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new connection token</returns>
        Task<string> CreateConnectionToken(string id, SessionId userSessionId, TokenVersion version = TokenVersion.V3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Crates a connection token for servers.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="serverId"></param>
        /// <returns></returns>
        Task<string> CreateServerConnectionToken(string gameSessionId, string serverId);


        /// <summary>
        /// Tries to reserve a slot in a game session for a team.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="team"></param>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<GameSessionReservation?> CreateReservation(string gameSessionId, Plugins.Models.Team team, JObject args, CancellationToken cancellationToken);

        /// <summary>
        /// Cancels a reservation from a game session.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CancelReservation(string gameSessionId, string id, CancellationToken cancellationToken);

    }

    internal class GameSessions : IGameSessions
    {
        private readonly Lazy<IScenesManager> management;
        private readonly RecyclableMemoryStreamProvider _memoryStreamProvider;
        private readonly IEnvironment _env;
        private readonly Lazy<GameSessionProxy> s2SProxy;
        private readonly Lazy<IUserSessions> sessions;
        private readonly ISerializer serializer;
        private readonly JsonSerializer _jsonSerializer;

        public GameSessions(
            Lazy<IScenesManager> management,
            RecyclableMemoryStreamProvider memoryStreamProvider, 
            IEnvironment env,
            Lazy<GameSessionProxy> s2sProxy, 
            Lazy<IUserSessions> sessions,
            ISerializer serializer,
            JsonSerializer jsonSerializer)
        {
            this.management = management;
            _memoryStreamProvider = memoryStreamProvider;
            _env = env;
            s2SProxy = s2sProxy;
            this.sessions = sessions;
            this.serializer = serializer;
            _jsonSerializer = jsonSerializer;
        }

        public async Task Create(string template, string id, GameSessionConfiguration config, CancellationToken cancellationToken)
        {
            var appInfos = await _env.GetApplicationInfos();
            await management.Value.CreateOrUpdateSceneAsync(new Platform.Core.Models.SceneDefinition { 
                AccountId = appInfos.AccountId,
                Application = appInfos.ApplicationName,
                Id = id,
                PartitioningPolicy = Stormancer.Server.Cluster.Constants.PARTITIONING_POLICY_HASH,
                SceneType = template,
                ShardGroup = Stormancer.Server.Cluster.Constants.SHARDGROUP_DEFAULT,
                Public = false,
                IsPersistent = false,
                Metadata = JObject.FromObject(new { gameSession = config },_jsonSerializer ).ToDictionary()

            },false, cancellationToken);
        }

        public Task Create(string template, string id, GameSessionConfiguration config)
        {
            return Create(template, id, config, CancellationToken.None);
        }

        public async Task<string> CreateConnectionToken(string id, SessionId userSessionId, TokenVersion version, CancellationToken cancellationToken)
        {
            using (var stream = _memoryStreamProvider.GetStream())
            {
                var session = await sessions.Value.GetSessionById(userSessionId, cancellationToken);
                serializer.Serialize(session,(IBufferWriter<byte>) stream);
                return await TaskHelper.Retry(async (_, _) => version switch
                {
                    TokenVersion.V3 => await management.Value.CreateConnectionTokenAsync(id, stream.ToArray(), "stormancer/userSession",3),
                    TokenVersion.V1 => await management.Value.CreateConnectionTokenAsync(id, stream.ToArray(), "stormancer/userSession",1),
                    _ => throw new InvalidOperationException("Unhandled TokenVersion value")

                }, RetryPolicies.IncrementalDelay(4, TimeSpan.FromSeconds(200)), CancellationToken.None, ex => true,true) ;
            }
        }

        public Task<GameSessionReservation?> CreateReservation(string gameSessionId, Team team, JObject args, CancellationToken cancellationToken)
        {
            return s2SProxy.Value.CreateReservation(gameSessionId, team, args, cancellationToken);
        }

        public Task CancelReservation(string gameSessionId, string reservationId, CancellationToken cancellationToken)
        {
            return s2SProxy.Value.CancelReservation(gameSessionId, reservationId, cancellationToken);
        }

        public Task<string> CreateServerConnectionToken(string gameSessionId, string serverId)
        {
            return management.Value.CreateConnectionTokenAsync(gameSessionId, Encoding.UTF8.GetBytes(serverId), "application/server-id");
        }
    }
}
