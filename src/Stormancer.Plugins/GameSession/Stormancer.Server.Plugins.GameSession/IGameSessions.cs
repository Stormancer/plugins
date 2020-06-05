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
using Stormancer.Server.Plugins.Management;
using Stormancer.Server.Plugins.Users;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    public enum TokenVersion
    {
        V1,
        V3
    }

    public interface IGameSessions
    {
        Task Create(string template, string id, GameSessionConfiguration config);
        /// <summary>
        /// Create a connection token for a given user and game session scene.
        /// </summary>
        /// <param name="id">Id of the game session's scene</param>
        /// <param name="userSessionId">Session Id of the target user</param>
        /// <param name="version">Version of the resulting token payload</param>
        /// <returns>The new connection token</returns>
        Task<string> CreateConnectionToken(string id, string userSessionId, TokenVersion version = TokenVersion.V3);
        Task<string> CreateServerConnectionToken(string gameSessionId, Guid serverId);
    }

    internal class GameSessions : IGameSessions
    {
        private readonly ManagementClientProvider management;
        private readonly IUserSessions sessions;
        private readonly ISerializer serializer;

        public GameSessions(ManagementClientProvider management, IUserSessions sessions, ISerializer serializer)
        {
            this.management = management;
            this.sessions = sessions;
            this.serializer = serializer;
        }

        public Task Create(string template, string id, GameSessionConfiguration config)
        {
            return management.CreateScene(id, template, false, false, JObject.FromObject(new { gameSession = config }));
        }

        public async Task<string> CreateConnectionToken(string id, string userSessionId, TokenVersion version)
        {
            using (var stream = new MemoryStream())
            {
                var session = await sessions.GetSessionById(userSessionId);
                serializer.Serialize(session, stream);
                return await TaskHelper.Retry(async () => version switch
                {
                    TokenVersion.V3 => await management.CreateConnectionToken(id, stream.ToArray(), "stormancer/userSession"),
                    //TokenVersion.V1 => await management.CreateConnectionTokenV1(id, stream.ToArray(), "stormancer/userSession"),
                    _ => throw new InvalidOperationException("Unhandled TokenVersion value")

                }, RetryPolicies.IncrementalDelay(4, TimeSpan.FromSeconds(200)), CancellationToken.None, ex => true);
            }
        }

        public Task<string> CreateServerConnectionToken(string gameSessionId, Guid serverId)
        {
            return management.CreateConnectionToken(gameSessionId, serverId.ToByteArray(), "application/server-id");
        }
    }
}
