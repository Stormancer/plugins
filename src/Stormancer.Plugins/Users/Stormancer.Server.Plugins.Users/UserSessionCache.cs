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

using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    class UserSessionCache
    {
        //(userId=> sessionId),(platformId=>sessionId)
        private readonly ConcurrentDictionary<string, string> _userIdToSessionId = new ConcurrentDictionary<string, string>();
        //(sessionId =>session)
        private readonly ConcurrentDictionary<string, Task<Session?>> _sessions = new ConcurrentDictionary<string, Task<Session?>>();
        private readonly ISceneHost scene;
        private readonly ISerializer serializer;

        private readonly ILogger logger;

        public UserSessionCache(ISceneHost scene, ISerializer serializer, ILogger logger)
        {
            this.scene = scene;
            this.serializer = serializer;

            this.logger = logger;
        }

        public Task OnConnected(IScenePeerClient peer)
        {
            if (peer.ContentType == "stormancer/userSession")
            {
                using (var stream = new MemoryStream(peer.UserData))
                {
                    var session = serializer.Deserialize<Session>(stream);
                    _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (uid, old) => session.SessionId);
                    _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
                    _sessions.AddOrUpdate(session.SessionId, Task.FromResult(session)!, (uid, old) => Task.FromResult(session)!);
                }
            }
            else
            {

                async Task<Session?> getSessionAndManageDictionary(string sessionId)
                {
                    var session = await GetSessionFromAuthenticator(peer.SessionId, "");//Use default authenticator for cluster
                    if (session == null)
                    {
                        _sessions.TryRemove(sessionId, out _);

                    }
                    return session;
                }

                _sessions.AddOrUpdate(peer.SessionId, (id) => getSessionAndManageDictionary(peer.SessionId), (id, old) => getSessionAndManageDictionary(peer.SessionId));

            }
            return Task.CompletedTask;
        }

        public async Task OnDisconnected(IScenePeerClient peer)
        {
            if (_sessions.TryRemove(peer.SessionId, out var sessionAsync))
            {
                var session = await sessionAsync;
                if (session?.User?.Id != null)
                {
                    _userIdToSessionId.TryRemove(session.User.Id, out _);
                    _userIdToSessionId.TryRemove(session.platformId.ToString(), out _);
                }
            }
        }

        public async Task<IScenePeerClient?> GetPeerByUserId(string userId, string authType)
        {
            var session = await GetSessionByUserId(userId, true, authType, false);
            if (session != null)
            {
                var peer = scene.RemotePeers.FirstOrDefault(p => p.SessionId == session.SessionId);
                if (peer == null)
                {
                    _userIdToSessionId.TryRemove(userId, out _);
                    _userIdToSessionId.TryRemove(session.platformId.ToString(), out _);
                }
                return peer;
            }
            else
            {
                return null;
            }

        }

        public async Task<Session?> GetSessionBySessionId(string sessionId, bool allowRemoteFetch, string authType, bool forceRefresh)
        {
            if (allowRemoteFetch)
            {
                return await _sessions.AddOrUpdate(sessionId,
                    id => GetSessionFromAuthenticator(id, authType),
                    (id, session) =>
                    {
                        if (session.IsCompleted && (forceRefresh || (session.Result != null && session.Result.MaxAge <= DateTimeOffset.UtcNow)))
                        {
                            return GetSessionFromAuthenticator(id, authType);
                        }
                        return session;
                    });
            }
            else
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    return await session;
                }
                else
                {
                    return null;
                }
            }
        }

        private async Task<Session?> GetSessionFromAuthenticatorByUserId(string userId, string authType)
        {
            var session = await AuthenticatorRpc<Session, string>("usersession.getsessionbyuserid", authType, userId);
            if (session != null)
            {
                _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (id, old) => session.SessionId);
                _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
                await _sessions.AddOrUpdate(session.SessionId, Task.FromResult(session), (id, old) => Task.FromResult(session));
            }
            return session;
        }

        private async Task<Session?> GetSessionFromAuthenticator(string sessionId, string authType)
        {
            var session = await AuthenticatorRpc<Session, string>("usersession.getsessionbyid", authType, sessionId);
            if (session != null)
            {
                _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (id, old) => session.SessionId);
                _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
            }
            return session;
        }

        private async Task<Dictionary<PlatformId, Session?>> GetSessionsFromAuthenticatorByPlatformIds(IEnumerable<PlatformId> platformIds, string authType)
        {
            var sessions = await AuthenticatorRpc<Dictionary<PlatformId, Session?>, IEnumerable<PlatformId>>("usersession.getsessionsbyplatformids", authType, platformIds);
            foreach (var session in sessions.Values)
            {
                if (session != null)
                {
                    _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (id, old) => session.SessionId);
                    _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
                    _ = _sessions.AddOrUpdate(session.SessionId, Task.FromResult(session), (id, old) => Task.FromResult(session));
                }
            }
            return sessions;
        }

        private async Task<Dictionary<string, Session?>> GetSessionsFromAuthenticatorBySessionIds(IEnumerable<string> sessionIds, string authType)
        {
            var sessions = await AuthenticatorRpc<Dictionary<string, Session?>, IEnumerable<string>>("UserSession.GetSessionsbySessionIds", authType, sessionIds);
            foreach (var session in sessions.Values)
            {
                _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (id, old) => session.SessionId);
                _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
                await _sessions.AddOrUpdate(session.SessionId, Task.FromResult(session), (id, old) => Task.FromResult(session));
            }
            return sessions;
        }

        public Task<Session?> GetSessionByUserId(string userId, bool allowRemoteFetch, string authType, bool forceRefresh)
        {
            if (_userIdToSessionId.TryGetValue(userId, out var sessionId))
            {
                return GetSessionBySessionId(sessionId, allowRemoteFetch, authType, forceRefresh);
            }
            else if (allowRemoteFetch)
            {
                return GetSessionFromAuthenticatorByUserId(userId, authType);
            }
            else
            {
                return Task.FromResult<Session?>(null);
            }
        }

        public async Task<Dictionary<PlatformId, Session?>> GetSessionsByPlatformIds(IEnumerable<PlatformId> platformIds, bool allowRemoteFetch, string authType, bool forceRefresh)
        {
            var result = new Dictionary<PlatformId, Session?>();
            var idsToQuery = new List<PlatformId>();

            foreach (var id in platformIds)
            {
                if (_userIdToSessionId.TryGetValue(id.ToString(), out var sessionId))
                {
                    result[id] = await GetSessionBySessionId(sessionId, allowRemoteFetch, authType, forceRefresh);
                }
                else
                {
                    idsToQuery.Add(id);
                }
            }

            if (allowRemoteFetch)
            {
                foreach (var session in await GetSessionsFromAuthenticatorByPlatformIds(idsToQuery, authType))
                {
                    result[session.Key] = session.Value;
                }
            }

            return result;
        }

        private async Task<TOut> AuthenticatorRpc<TOut, TArg1>(string route, string type, TArg1 arg1)
        {
            using (var scope = scene.DependencyResolver.CreateChild(global::Stormancer.Server.Plugins.API.Constants.ApiRequestTag))
            {
                var rpc = scope.Resolve<RpcService>();
                var locator = scope.Resolve<IServiceLocator>();

                var packet = await rpc.Rpc(route, new MatchSceneFilter(await locator.GetSceneId("stormancer.authenticator" + (string.IsNullOrEmpty(type) ? "" : "-" + type), "")), s => serializer.Serialize(arg1, s), PacketPriority.MEDIUM_PRIORITY).LastOrDefaultAsync();
                if (packet != null)
                {
                    using (packet.Stream)
                    {
                        return serializer.Deserialize<TOut>(packet.Stream);
                    }
                }
                else
                {
                    return default(TOut);
                }
            }
        }

        public async Task<Dictionary<string, Session?>> GetSessionsByIds(IEnumerable<string> sessionIds, bool allowRemoteFetch, string authType, bool forceRefresh)
        {
            var result = new Dictionary<string, Session?>();
            IEnumerable<string> idsToQuery;

            if (forceRefresh)
            {
                idsToQuery = sessionIds;
            }
            else
            {
                var ids = new List<string>();

                foreach (var id in sessionIds)
                {
                    if (!_sessions.TryGetValue(id, out var session))
                    {
                        ids.Add(id);
                    }
                    else
                    {
                        result.Add(id, await session);
                    }
                }
                idsToQuery = ids;
            }

            if (allowRemoteFetch)
            {
                var getSessionsTask = GetSessionsFromAuthenticatorBySessionIds(idsToQuery, authType);

                async Task<Session?> GetSession(string sessionId, Task<Dictionary<string, Session?>> sessionsTask)
                {
                    var results = await sessionsTask;

                    if (results.TryGetValue(sessionId, out var session) && session != null)
                    {
                        _userIdToSessionId.AddOrUpdate(session.User.Id, session.SessionId, (uid, old) => session.SessionId);
                        _userIdToSessionId.AddOrUpdate(session.platformId.ToString(), session.SessionId, (pid, old) => session.SessionId);
                        return session;
                    }
                    else
                    {
                        _sessions.TryRemove(sessionId, out _);
                        return null;
                    }
                }

                foreach (var id in idsToQuery)
                {
                    _ = _sessions.AddOrUpdate(id, GetSession(id, getSessionsTask), (uid, old) => GetSession(id, getSessionsTask));

                }

                foreach (var entry in await getSessionsTask)
                {
                    result.Add(entry.Key, entry.Value);
                }
            }

            return result;

        }
    }
}
