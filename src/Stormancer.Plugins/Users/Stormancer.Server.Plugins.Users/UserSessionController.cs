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
using System.IO.Pipelines;
using MessagePack;

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
        private readonly IUserService _users;

        public UserSessionController(IUserSessions sessions,
            IUserService users,
            ISerializer serializer, ISceneHost scene, IEnvironment env, ILogger logger, IConfiguration configuration)
        {

            _logger = logger;
            this.configuration = configuration;
            _environment = env;
            _sessions = sessions;
            _users = users;
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
        public async Task<IEnumerable<SessionId>> GetPeers(string userId, CancellationToken cancellationToken)
        {

            var result = await _sessions.GetPeers(userId, cancellationToken);

            return result;
        }


        [S2SApi(GeneratePrivateImpl = true)]
        public async Task<bool> IsAuthenticated(SessionId sessionId, CancellationToken cancellationToken)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
            if (peer == null)
            {
                return false;
            }

            return await _sessions.IsAuthenticated(peer, cancellationToken);

        }

        [S2SApi]
        public Task UpdateUserData(SessionId sessionId, JObject data, CancellationToken cancellationToken)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);

            if (peer == null)
            {
                throw new ClientException("NotFound");
            }

            return _sessions.UpdateUserData(peer, data, cancellationToken);
        }



        [S2SApi]
        public Task<IEnumerable<Session>> GetSessionsByUserId(string userId, CancellationToken cancellationToken)
        {
            return _sessions.GetSessions(new PlatformId { Platform = Constants.PROVIDER_TYPE_STORMANCER, PlatformUserId = userId }, cancellationToken);
        }


        [S2SApi]
        public Task<IEnumerable<Session>> GetSessionsByPlatformId(PlatformId platformId, CancellationToken cancellationToken)
        {
            return _sessions.GetSessions(platformId, cancellationToken);
        }

        [S2SApi]
        public Task<Session?> GetSessionById(SessionId sessionId, CancellationToken cancellationToken)
        {
            var session = _sessions.GetSessionById(sessionId, cancellationToken);
#if DEBUG
            if (session == null)
            {
                _logger.Log(LogLevel.Warn, "usersession", $"Get session failed for id {sessionId}", new { });
            }
#endif
            return session!;
        }



        [S2SApi]
        public Task<Dictionary<SessionId, Session?>> GetSessionsbySessionIds(IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken)
        {
            return _sessions.GetSessions(sessionIds, cancellationToken);
        }

        [S2SApi(GeneratePrivateImpl = true)]
        public Task UpdateSessionData(SessionId sessionId, string key, [S2SContextUsage(S2SRequestContextUsage.Write)] IS2SRequestContext ctx)
        {

            using var memoryStream = new MemoryStream();
            ctx.Reader.AsStream().CopyTo(memoryStream);

            return _sessions.UpdateSessionData(sessionId, key, memoryStream.ToArray(), ctx.CancellationToken);
        }

        [S2SApi]
        public Task<byte[]?> GetSessionData(SessionId sessionId, string key, CancellationToken cancellationToken)
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
        public Task<string?> UpdateUserHandle(string userId, string newHandle, CancellationToken cancellationToken)
        {
            return _users.UpdateUserHandleAsync(userId, newHandle, cancellationToken);
        }

        [S2SApi]
        public Task KickUser(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken)
        {
            return _sessions.KickUser(userIds, reason, cancellationToken);
        }

        [S2SApi]
        public async Task SendRequest(string operationName, string senderUserId, string recipientUserId, [S2SContextUsage(S2SRequestContextUsage.Read | S2SRequestContextUsage.Write)] IS2SRequestContext ctx)
        {
            await using var rq = _sessions.SendRequest(operationName, senderUserId, recipientUserId, ctx.CancellationToken);

            var t1 = ctx.Reader.TryCopyToAsync(rq.Writer, true, ctx.CancellationToken);
            var t2 = rq.Reader.TryCopyToAsync(ctx.Writer, true, ctx.CancellationToken);

            await t2;
            await t1;

        }

        [S2SApi]
        public Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken)
        {
            return _sessions.GetAuthenticatedUsersCount(cancellationToken);
        }

        [S2SApi]
        public IAsyncEnumerable<Session> GetSessionsAsync(CancellationToken cancellationToken)
        {
            return _sessions.GetSessionsAsync(cancellationToken);
        }

        /// <summary>
        /// Gets the number of users currently authenticated.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
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
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
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

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<JObject> GetUserOptions(string key, RequestContext<IScenePeerClient> ctx)
        {
            var user = await _sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);
            if (user == null)
            {
                throw new ClientException("notFound");
            }
            if (!(user.UserData.TryGetValue("options", out var token) && token is JObject options))
            {
                options = new JObject();
                user.UserData["options"] = options;
            }
            return options;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UpdateUserOptions(string key, JObject value, RequestContext<IScenePeerClient> ctx)
        {
            var user = await _sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);
            if (user == null)
            {
                throw new ClientException("notAuthenticated");
            }
            try
            {
                await _sessions.UpdateUserOptionsAsync(user.Id, key, value, ctx.CancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message == "notfound")
            {
                throw new ClientException("userNotPersistent");
            }

        }

        [S2SApi]
        public Task UpdateUserOptionsAsync(string userId, string key, JObject value, CancellationToken cancellationToken)
        {
            return _sessions.UpdateUserOptionsAsync(userId, key, value, cancellationToken);
        }

        [S2SApi]
        public Task<Dictionary<string, UserSessionInfos>> GetDetailedUserInformationsByIdentity(string platform, IEnumerable<string> ids, CancellationToken cancellationToken)
        {
            return _sessions.GetDetailedUserInformationsByIdentityAsync(platform, ids, cancellationToken);
        }

    }




}