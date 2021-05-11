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
using Stormancer.Server.Plugins.API;
using Stormancer;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Stormancer.Server.Plugins.Users
{
    [Service(Named = false, ServiceType = Constants.SERVICE_TYPE)]
    internal class UserSessionController : ControllerBase
    {
        private static Lazy<byte[]>? _key;
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;
        private readonly IConfiguration configuration;
        private readonly IEnvironment _environment;
        private readonly IUserSessions _sessions;



        public UserSessionController(IUserSessions sessions, ISerializer serializer, ISceneHost scene, IEnvironment env, ILogger logger, IConfiguration configuration)
        {

            _logger = logger;
            this.configuration = configuration;
            _environment = env;
            _sessions = sessions;
            _serializer = serializer;
            _scene = scene;
            if (_key == null)
            {
                _key = new Lazy<byte[]>(() =>
                  {
                      var key = configuration.GetValue<string?>("security.tokenKey", null);
                      if (key == null)
                      {
                          var bytes = new byte[32];
                          System.Security.Cryptography.RandomNumberGenerator.Fill(new Span<byte>(bytes));
                          return bytes;
                      }
                      else
                      {
                          return System.Convert.FromBase64String(key);
                      }
                  });
            }
        }

        /// <summary>
        /// Gets the currently connected peer authenticated as an userId.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [S2SApi(GeneratePrivateImpl = true)]
        public async Task<string?> GetPeer(string userId, CancellationToken cancellationToken)
        {

            var result = await _sessions.GetPeer(userId, cancellationToken);

            return result?.SessionId;
        }


        [S2SApi(GeneratePrivateImpl = true)]
        public async Task<bool> IsAuthenticated(string sessionId, CancellationToken cancellationToken)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
            if (peer == null)
            {
                return false;
            }

            return await _sessions.IsAuthenticated(peer, cancellationToken);

        }

        [S2SApi]
        public Task UpdateUserData(string sessionId, JObject data, CancellationToken cancellationToken)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);

            if (peer == null)
            {
                throw new ClientException("NotFound");
            }

            return _sessions.UpdateUserData(peer, data, cancellationToken);
        }

        [S2SApi]
        public Task<PlatformId> GetPlatformId(string userId, CancellationToken cancellationToken)
        {
            return _sessions.GetPlatformId(userId, cancellationToken);
        }

        [S2SApi]
        public Task<Session?> GetSessionByUserId(string userId, CancellationToken cancellationToken)
        {
            return _sessions.GetSessionByUserId(userId, cancellationToken);
        }

        [S2SApi]
        public Task<Session?> GetSessionById(string sessionId, CancellationToken cancellationToken)
        {
            return _sessions.GetSessionById(sessionId, cancellationToken);
        }

        [S2SApi]
        public Task<Dictionary<PlatformId, Session?>> GetSessionsByPlatformIds(IEnumerable<PlatformId> platformIds, CancellationToken cancellationToken)
        {

            return _sessions.GetSessions(platformIds, cancellationToken);


        }

        [S2SApi]
        public Task<Dictionary<string, Session?>> GetSessionsbySessionIds(IEnumerable<string> sessionIds, CancellationToken cancellationToken)
        {
            return _sessions.GetSessions(sessionIds, cancellationToken);
        }

        [S2SApi(GeneratePrivateImpl = true)]
        public Task UpdateSessionData(string sessionId, string key, [S2SContextUsage(S2SRequestContextUsage.Read)] IS2SRequestContext ctx)
        {

            using var memoryStream = new MemoryStream();
            ctx.Reader.AsStream().CopyTo(memoryStream);

            return _sessions.UpdateSessionData(sessionId, key, memoryStream.ToArray(), ctx.CancellationToken);
        }

        [S2SApi]
        public Task<byte[]?> GetSessionData(string sessionId, string key, CancellationToken cancellationToken)
        {
            return _sessions.GetSessionData(sessionId, key, cancellationToken);

        }


        [S2SApi]
        public Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return _sessions.GetUsers(userIds.ToArray(), cancellationToken);
        }

        [S2SApi]
        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            return _sessions.Query(query, take, skip, cancellationToken);
        }

        [S2SApi]
        public Task<string> UpdateUserHandle(string userId, string newHandle, bool appendHash, CancellationToken cancellationToken)
        {
            return _sessions.UpdateUserHandle(userId, newHandle, appendHash, cancellationToken);
        }

        [S2SApi]
        public Task KickUser(string userId, string reason, CancellationToken cancellationToken)
        {
            return _sessions.KickUser(userId, reason, cancellationToken);
        }

        [S2SApi]
        public async Task SendRequest(string operationName, string senderUserId, string recipientUserId, [S2SContextUsage(S2SRequestContextUsage.Read | S2SRequestContextUsage.Write)] IS2SRequestContext ctx)
        {
            await using var rq = _sessions.SendRequest(operationName, senderUserId, recipientUserId, ctx.CancellationToken);

            await Task.WhenAll(ctx.Reader.CopyToAsync(rq.Writer, ctx.CancellationToken), rq.Reader.CopyToAsync(ctx.Writer));

        }

        [S2SApi]
        public Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken)
        {
            return _sessions.GetAuthenticatedUsersCount(cancellationToken);
        }

        /// <summary>
        /// Gets the number of users currently authenticated.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [S2SApi]
        public Task<int> GetAuthenticatedUsersCountPublic(CancellationToken cancellationToken)
        {
            return _sessions.GetAuthenticatedUsersCount(cancellationToken);
        }

        /// <summary>
        /// Creates a bearer token that contains the user's session id.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreateUserBearerToken(RequestContext<IScenePeerClient> ctx)
        {
            Debug.Assert(_key != null);
            var session = await _sessions.GetSession(ctx.RemotePeer,ctx.CancellationToken);
            return Jose.JWT.Encode(new Dictionary<string, string?> { { "userId", session?.User?.Id } }, _key.Value, Jose.JwsAlgorithm.HS256);
        }

        /// <summary>
        /// Check the signature of a bearer token and get the user id it was created for.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public string? GetUserIdFromBearerToken(string token)
        {
            Debug.Assert(_key != null);
            return Jose.JWT.Decode<Dictionary<string, string?>>(token, _key.Value)["userId"];
        }
    }
}