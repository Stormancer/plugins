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
        /// <returns></returns>
        [S2SApi(GeneratePrivateImpl = true)]
        public async Task<string?> GetPeer(string userId)
        {

            var result = await _sessions.GetPeer(userId);

            return result?.SessionId;

        }


        [S2SApi(GeneratePrivateImpl = true)]
        public async Task<bool> IsAuthenticated(string sessionId)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
            if (peer == null)
            {
                return false;
            }

            return await _sessions.IsAuthenticated(peer);

        }

        [S2SApi]
        public Task UpdateUserData(string sessionId, JObject data)
        {

            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);

            if (peer == null)
            {
                throw new ClientException("NotFound");
            }

            return _sessions.UpdateUserData(peer, data);
        }

        [S2SApi]
        public Task<PlatformId> GetPlatformId(string userId)
        {
            return _sessions.GetPlatformId(userId);
        }

        [S2SApi]
        public Task GetSessionByUserId(string userId)
        {      
            return _sessions.GetSessionByUserId(userId);
        }

        [S2SApi]
        public Task<Session?> GetSessionById(string sessionId)
        {        
            return _sessions.GetSessionById(sessionId);
        }

        [S2SApi]
        public Task<Dictionary<PlatformId,Session?>> GetSessionsByPlatformIds(IEnumerable<PlatformId> platformIds)
        {
         
            return _sessions.GetSessions(platformIds);

          
        }

        [S2SApi]
        public Task<Dictionary<string, Session?>> GetSessionsbySessionIds(IEnumerable<string> sessionIds)
        {
            return _sessions.GetSessions(sessionIds);
        }

        [S2SApi(GeneratePrivateImpl =true)]
        public Task UpdateSessionData(string sessionId, string key,[S2SContextUsage(S2SRequestContextUsage.Read)] IS2SRequestContext ctx)
        {
           
            using var memoryStream = new MemoryStream();
            ctx.Reader.AsStream().CopyTo(memoryStream);

            return _sessions.UpdateSessionData(sessionId, key, memoryStream.ToArray());
        }

        [S2SApi]
        public Task<byte[]?> GetSessionData(string sessionId, string key)
        {        
            return _sessions.GetSessionData(sessionId, key);

        }


        [S2SApi]
        public Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds)
        {
            return _sessions.GetUsers(userIds.ToArray());
        }

        [S2SApi]
        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip)
        {
            return _sessions.Query(query, take, skip);
        }

        [S2SApi]
        public Task UpdateUserHandle(string userId, string newHandle, bool appendHash)
        {
            return _sessions.UpdateUserHandle(userId, newHandle, appendHash);
        }

        [S2SApi]
        public Task KickUser(string userId, string reason)
        {
            return _sessions.KickUser(userId, reason);
        }

        [S2SApi]
        public async Task SendRequest(string operationName, string senderUserId, string recipientUserId, [S2SContextUsage(S2SRequestContextUsage.Read| S2SRequestContextUsage.Write)] IS2SRequestContext ctx)
        {
            using var rq = _sessions.SendRequest(operationName, senderUserId, recipientUserId, ctx.CancellationToken);

            await Task.WhenAll(ctx.Reader.CopyToAsync(rq.Writer, ctx.CancellationToken),rq.Reader.CopyToAsync(ctx.Writer));
            
        }
        [S2SApi]
        public Task<int> GetAuthenticatedUsersCount()
        {
            return _sessions.GetAuthenticatedUsersCount();
        }

        [S2SApi]
        public Task<int> GetAuthenticatedUsersCountPublic()
        {
            return _sessions.GetAuthenticatedUsersCount();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreateUserBearerToken(RequestContext<IScenePeerClient> ctx)
        {
            Debug.Assert(_key != null);
            var session = await _sessions.GetSession(ctx.RemotePeer);
            return Jose.JWT.Encode(new Dictionary<string, string?> { { "userId", session?.User?.Id } }, _key.Value, Jose.JwsAlgorithm.HS256);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public string? GetUserIdFromBearerToken(string token)
        {
            Debug.Assert(_key != null);
            return Jose.JWT.Decode<Dictionary<string, string?>>(token, _key.Value)["userId"];
        }
    }
}