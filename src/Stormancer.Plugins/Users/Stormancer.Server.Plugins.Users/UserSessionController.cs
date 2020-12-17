﻿// MIT License
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
        public async Task GetPeer(RequestContext<IScenePeer> rq)
        {
            var userId = _serializer.Deserialize<string>(rq.InputStream);
            var result = await _sessions.GetPeer(userId);

            await rq.SendValue(s => _serializer.Serialize(result?.SessionId, s));
        }



        public async Task IsAuthenticated(RequestContext<IScenePeer> rq)
        {
            var sessionId = _serializer.Deserialize<string>(rq.InputStream);
            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
            var isAuthenticated = await _sessions.IsAuthenticated(peer);
            await rq.SendValue(s => _serializer.Serialize(isAuthenticated, s));
        }

        public async Task UpdateUserData(RequestContext<IScenePeer> rq)
        {
            var sessionId = _serializer.Deserialize<string>(rq.InputStream);
            var peer = _scene.RemotePeers.FirstOrDefault(p => p.SessionId == sessionId);
            var data = _serializer.Deserialize<JObject>(rq.InputStream);

            await _sessions.UpdateUserData(peer, data);
        }

        public async Task GetPlatformId(RequestContext<IScenePeer> rq)
        {
            var userId = _serializer.Deserialize<string>(rq.InputStream);
            var platformId = await _sessions.GetPlatformId(userId);

            await rq.SendValue(s => _serializer.Serialize(platformId, s));
        }

        public async Task GetSessionByUserId(RequestContext<IScenePeer> rq)
        {
            var userId = _serializer.Deserialize<string>(rq.InputStream);
            var session = await _sessions.GetSessionByUserId(userId);

            await rq.SendValue(s => _serializer.Serialize(session, s));
        }

        public async Task GetSessionById(RequestContext<IScenePeer> rq)
        {
            var peerId = _serializer.Deserialize<string>(rq.InputStream);
            var session = await _sessions.GetSessionById(peerId);

            await rq.SendValue(s => _serializer.Serialize(session, s));
        }

        public async Task GetSessionsByPlatformIds(RequestContext<IScenePeer> rq)
        {
            var platformIds = _serializer.Deserialize<IEnumerable<PlatformId>>(rq.InputStream);
            var sessions = await _sessions.GetSessions(platformIds);

            await rq.SendValue(s => _serializer.Serialize(sessions, s));
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Task<Dictionary<string, Session?>> GetSessionsbySessionIds(IEnumerable<string> sessionIds)
        {
            return _sessions.GetSessions(sessionIds);
        }

        public async Task UpdateSessionData(RequestContext<IScenePeer> rq)
        {
            var sessionId = _serializer.Deserialize<string>(rq.InputStream);
            var key = _serializer.Deserialize<string>(rq.InputStream);
            using var memoryStream = new MemoryStream();
            rq.InputStream.CopyTo(memoryStream);


            await _sessions.UpdateSessionData(sessionId, key, memoryStream.ToArray());
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public async Task<byte[]?> GetSessionData(RequestContext<IScenePeer> rq)
        {
            var sessionId = _serializer.Deserialize<string>(rq.InputStream);
            var key = _serializer.Deserialize<string>(rq.InputStream);

            var value = await _sessions.GetSessionData(sessionId, key);

            return value;
        }

        //public async Task DecodeBearerToken(RequestContext<IScenePeer> rq)
        //{
        //    var token = _serializer.Deserialize<string>(rq.InputStream);
        //    var app = await _environment.GetApplicationInfos();
        //    var data = TokenGenerator.DecodeToken<BearerTokenData>(token, app.PrimaryKey);
        //    await rq.SendValue(s=> _serializer.Serialize(data,s));
        //}

        //[Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        //public async Task<Session> GetSessionByBearerToken(string bearerToken)
        //{
        //    var app = await _environment.GetApplicationInfos();
        //    var data = TokenGenerator.DecodeToken<BearerTokenData>(bearerToken, app.PrimaryKey);
        //    if (data == null)
        //    {
        //        throw new ClientException("bearerToken.invalidToken");
        //    }
        //    return await _sessions.GetSessionById(data.SessionId);


        //}

        //[Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        //public async Task<string> GetBearerToken(string sessionId)
        //{
        //    var app = await _environment.GetApplicationInfos();
        //    var session = await _sessions.GetSessionById(sessionId);
        //    return TokenGenerator.CreateToken(new BearerTokenData { SessionId = sessionId, pid = session.platformId, userId = session.User.Id, IssuedOn = DateTime.UtcNow, ValidUntil = DateTime.UtcNow + TimeSpan.FromHours(1) }, app.PrimaryKey);
        //}

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Task<Dictionary<string, User>> GetUsers(IEnumerable<string> userIds)
        {
            return _sessions.GetUsers(userIds.ToArray());
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip)
        {
            return _sessions.Query(query, take, skip);
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Task UpdateUserHandle(string userId, string newHandle, bool appendHash)
        {
            return _sessions.UpdateUserHandle(userId, newHandle, appendHash);
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public async Task SendRequest(string operationName, string senderUserId, string recipientUserId, RequestContext<IScenePeer> ctx)
        {
            await foreach (var data in _sessions.SendRequest(operationName, senderUserId, recipientUserId, s => ctx.InputStream.CopyTo(s), ctx.CancellationToken).ToAsyncEnumerable())
            {
                await ctx.SendValue(stream => stream.Write(data, 0, data.Length));
            }
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Task<int> GetAuthenticatedUsersCount()
        {
            return _sessions.GetAuthenticatedUsersCount();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<int> GetAuthenticatedUsersCountPublic()
        {
            return _sessions.GetAuthenticatedUsersCount();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreateUserBearerToken()
        {
            Debug.Assert(_key != null);
            var session = await _sessions.GetSession(this.Request.RemotePeer);
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