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
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    internal class UserSessionProxy : IUserSessions
    {
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;
        private readonly IServiceLocator _locator;
        private readonly UserSessionCache cache;
        private readonly IEnvironment env;


        public UserSessionProxy(ISceneHost scene, ISerializer serializer, IEnvironment env, IServiceLocator locator, UserSessionCache cache)
        {

            _scene = scene;
            _serializer = serializer;
            _locator = locator;
            this.cache = cache;
            this.env = env;
        }

        private async Task<T> AuthenticatorRpc<T>(string? targetSessionId, string route, Action<Stream> writer, string type = "")
        {
            return await AuthenticatorRpc(targetSessionId, route, writer, CancellationToken.None, type).Select(p =>
            {
                using (p)
                {
                    return _serializer.Deserialize<T>(p.Stream);
                }
            }).LastOrDefaultAsync();
        }

        private async Task AuthenticatorRpc(string? targetSessionId, string route, Action<Stream> writer, string type = "")
        {
            await AuthenticatorRpc(targetSessionId, route, writer, CancellationToken.None, type).Select(p =>
            {
                using (p)
                {
                    return System.Reactive.Unit.Default;
                }
            }).LastOrDefaultAsync();
        }

        private IObservable<Packet<IScenePeer>> AuthenticatorRpc(string? targetSessionId, string route, Action<Stream> writer, CancellationToken cancellationToken, string type = "")
        {
            return Observable.FromAsync(async () =>
            {
                var session = targetSessionId != null ? await this.cache.GetSessionBySessionId(targetSessionId, false, string.Empty, false) : null;
                if (session != null)
                {
                    return session.AuthenticatorUrl;
                }
                else
                {
                    return await _locator.GetSceneId("stormancer.authenticator" + (string.IsNullOrEmpty(type) ? "" : "-" + type), "");
                }
            })
            .Select(sceneId => AuthenticatorRpcWithSceneId(sceneId, route, writer, cancellationToken))
            .Switch();
        }

        private IObservable<Packet<IScenePeer>> AuthenticatorRpcWithSceneId(string sceneId, string route, Action<Stream> writer, CancellationToken cancellationToken)
        {
            var rpc = _scene.DependencyResolver.Resolve<RpcService>();

            return rpc.Rpc(route, new MatchSceneFilter(sceneId), writer, PacketPriority.MEDIUM_PRIORITY, cancellationToken);
        }

        private async Task<Packet<IScenePeer>> AuthenticatorRpcWithSceneId(string sceneId, string route, Action<Stream> writer)
        {
            return await AuthenticatorRpcWithSceneId(sceneId, route, writer, CancellationToken.None);
        }

        private Task<string> GetSessionIdForUser(string userId)
        {
            return AuthenticatorRpc<string>(null, "usersession.getpeer", s => _serializer.Serialize(userId, s));


        }
        public Task<IScenePeerClient?> GetPeer(string userId)
        {
            return cache.GetPeerByUserId(userId, "");
        }

        public async Task<User?> GetUser(IScenePeerClient peer)
        {
            var session = await GetSessionById(peer.SessionId, false);
            return session?.User;

        }


        public Task<bool> IsAuthenticated(IScenePeerClient peer)
        {
            return AuthenticatorRpc<bool>(peer.SessionId, "usersession.isauthenticated", s => _serializer.Serialize(peer.SessionId, s));


        }

        public Task UpdateUserData<T>(IScenePeerClient peer, T data)
        {
            return AuthenticatorRpc(peer.SessionId, "usersession.updateuserdata", s =>
            {
                _serializer.Serialize(peer.SessionId, s);
                _serializer.Serialize(JObject.FromObject(data), s);
            });
        }

        public async Task<PlatformId> GetPlatformId(string userId)
        {
            var session = await cache.GetSessionByUserId(userId, true, "", false);
            if (session != null)
            {
                return session.platformId;
            }
            else
            {
                throw new InvalidOperationException("player not connected to the scene.");
            }

        }

        public Task<Session?> GetSessionByUserId(string userId, bool forceRefresh)
        {
            return cache.GetSessionByUserId(userId, true, "", forceRefresh);
            //var response = await AuthenticatorRpc("usersession.getsessionbyuserid", s => _serializer.Serialize(userId, s));

            //var result = _serializer.Deserialize<Session>(response.Stream);
            //response.Stream.Dispose();
            //return result;
        }

        public Task<Session?> GetSessionById(string sessionId, string authType, bool forceRefresh)
        {
            return cache.GetSessionBySessionId(sessionId, true, authType, forceRefresh);
            //var response = await AuthenticatorRpc("usersession.getsessionbyid", s => _serializer.Serialize(sessionId, s),authType);
            //if (response != null)
            //{
            //    using (response.Stream)
            //    {
            //        var result = _serializer.Deserialize<Session>(response.Stream);
            //        return result;
            //    }
            //}
            //else
            //{
            //    return null;
            //}
        }

        public Task<Session?> GetSessionById(string sessionId, bool forceRefresh)
        {
            return GetSessionById(sessionId, "", forceRefresh);
        }

        public async Task<Session?> GetSession(IScenePeerClient peer, bool forceRefresh)
        {
            return await GetSessionById(peer.SessionId, forceRefresh);
        }

        /// <summary>
        /// Get the player session from the active authenticator scene (returns null for players authenticated on an older deployment.
        /// </summary>
        /// <param name="platformId"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        public async Task<Session?> GetSession(PlatformId platformId, bool forceRefresh)
        {
            var session = await GetSessions(platformId.ToEnumerable(), forceRefresh);
            return session.Values.FirstOrDefault();
        }

        public Task UpdateSessionData(string sessionId, string key, byte[] data)
        {
            return AuthenticatorRpc(sessionId, "usersession.updatesessiondata", s =>
             {
                 _serializer.Serialize(sessionId, s);
                 _serializer.Serialize(key, s);
                 s.Write(data, 0, data.Length);
             });


        }

        public Task<byte[]?> GetSessionData(string sessionId, string key)
        {
            return AuthenticatorRpc<byte[]?>(sessionId, "UserSession.GetSessionData", s =>
             {
                 _serializer.Serialize(sessionId, s);
                 _serializer.Serialize(key, s);
             });


        }

        public async Task UpdateSessionData<T>(string sessionId, string key, T data)
        {
            await AuthenticatorRpc(sessionId, "usersession.updatesessiondata", s =>
             {
                 _serializer.Serialize(sessionId, s);
                 _serializer.Serialize(key, s);
                 _serializer.Serialize(data, s);
             });


            // Refresh the cache to get the new session data
            await GetSessionById(sessionId, true);
        }

        public async Task<T?> GetSessionData<T>(string sessionId, string key)
        {
            var bytes = await AuthenticatorRpc<byte[]?>(sessionId, "UserSession.GetSessionData", s =>
             {
                 _serializer.Serialize(sessionId, s);
                 _serializer.Serialize(key, s);
             });
            if (bytes != null)
            {
                using (var memStream = new MemoryStream(bytes))
                {
                    return _serializer.Deserialize<T>(memStream);
                }
            }
            else
            {
                return default;
            }

        }



        public Task<Dictionary<string, User?>> GetUsers(params string[] userIds)
        {
            return AuthenticatorRpc<Dictionary<string, User?>>(null, $"UserSession.{nameof(GetUsers)}", s =>
            {
                _serializer.Serialize(userIds, s);
            });


        }

        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip)
        {
            return AuthenticatorRpc<IEnumerable<User>>(null, $"UserSession.{nameof(Query)}", s =>
             {
                 _serializer.Serialize(query, s);
                 _serializer.Serialize(take, s);
                 _serializer.Serialize(skip, s);
             });
          
        }

        public Task<string> UpdateUserHandle(string userId, string newHandle, bool appendHash)
        {
            return AuthenticatorRpc<string>(null, $"UserSession.{nameof(UpdateUserHandle)}", s =>
             {
                 _serializer.Serialize(userId, s);
                 _serializer.Serialize(newHandle, s);
                 _serializer.Serialize(appendHash, s);
             });
        }

        public async Task KickUser(string userId, string reason)
        {
            await AuthenticatorRpc(null, $"UserSession.{nameof(KickUser)}", s =>
            {
                _serializer.Serialize(userId, s);
                _serializer.Serialize(reason, s);
            });
        }

        public IObservable<byte[]> SendRequest(string operationName, string senderUserId, string recipientUserId, Action<Stream> writer, CancellationToken cancellationToken)
        {
            return AuthenticatorRpc(null, $"UserSession.{nameof(SendRequest)}", s =>
            {
                _serializer.Serialize(operationName, s);
                _serializer.Serialize(senderUserId, s);
                _serializer.Serialize(recipientUserId, s);
                writer?.Invoke(s);
            }, cancellationToken)
            .Select(packet =>
            {
                using var stream = new MemoryStream();
                packet.Stream.CopyTo(stream);
                return stream.ToArray();
            });
        }

        public Task<Dictionary<PlatformId, Session?>> GetSessions(IEnumerable<PlatformId> platformIds, bool forceRefresh = false)
        {
            return cache.GetSessionsByPlatformIds(platformIds, true, "", forceRefresh);
        }

        public Task<int> GetAuthenticatedUsersCount()
        {
           return AuthenticatorRpc<int>(null, $"UserSession.{nameof(GetAuthenticatedUsersCount)}", s => { });
          
        }

        public Task<Dictionary<string, Session?>> GetSessions(IEnumerable<string> sessionIds, bool forceRefresh = false)
        {
            return cache.GetSessionsByIds(sessionIds, true, "", forceRefresh);
        }
    }
}