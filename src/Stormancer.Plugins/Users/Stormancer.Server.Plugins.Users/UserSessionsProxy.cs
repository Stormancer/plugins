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
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    internal class UserSessionImpl : IUserSessions
    {
        private readonly UserSessionProxy proxy;
        private readonly IClusterSerializer _clusterSerializer;
        private readonly ISerializer _clientSerializer;
        private readonly ISceneHost scene;


        private static MemoryCache<SessionId,Session> sessionCache = new MemoryCache<SessionId,Session>();
        private int CACHE_DURATION_SECONDS = 30;
        public UserSessionImpl(UserSessionProxy proxy, IClusterSerializer serializer,ISerializer clientSerializer, ISceneHost scene)
        {
            this.proxy = proxy;
            this._clusterSerializer = serializer;
            _clientSerializer = clientSerializer;
            this.scene = scene;
        }

        public Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken)
        {
            return proxy.GetAuthenticatedUsersCount(cancellationToken);
        }

      

        public Task<Session?> GetSession(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            return GetSessionById(peer.SessionId, cancellationToken);
        }


        public async Task<Session?> GetSessionById(SessionId sessionId, CancellationToken cancellationToken)
        {
            var session = await sessionCache.Get(sessionId, async (id) =>
            {
                var session = await proxy.GetSessionById(sessionId, cancellationToken);

                return (session, TimeSpan.FromSeconds(CACHE_DURATION_SECONDS));
            });

            if (session == null)
            {
                sessionCache.Remove(sessionId);
            }

            return session;
        }

        

        public async Task<T?> GetSessionData<T>(SessionId sessionId, string key, CancellationToken cancellationToken)
        {
            var buffer = await proxy.GetSessionData(sessionId, key, cancellationToken);
            if (buffer == null)
            {
                return default;
            }
            var stream = new ReadOnlySequence<byte>(buffer);
            if(_clusterSerializer.TryDeserialize<T>(stream,out var value,out _))
            {
                return value;
            }
            else
            {
                return default;
            }
        }

        public Task<byte[]?> GetSessionData(SessionId sessionId, string key, CancellationToken cancellationToken)
        {
            return proxy.GetSessionData(sessionId, key, cancellationToken);
        }

       

        public async Task<Dictionary<SessionId, Session?>> GetSessions(IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken)
        {
            var entries = sessionCache.GetMany(sessionIds, (ids) =>
            {
                Dictionary<SessionId, Task<(Session?, TimeSpan)>> result = new();

                var task = proxy.GetSessionsbySessionIds(sessionIds, cancellationToken);

                async Task<(Session?, TimeSpan)> GetEntryAsync(SessionId id, Task<Dictionary<SessionId, Session?>> t)
                {
                    var r = await t;
                    return (r[id], TimeSpan.FromSeconds(CACHE_DURATION_SECONDS));

                }
                foreach (var sessionId in ids)
                {
                    result[sessionId] = GetEntryAsync(sessionId, task);
                }


                return result;
            });

            await Task.WhenAll(entries.Values);
            return entries.ToDictionary(kvp =>kvp.Key, kvp => kvp.Value.Result);

        }

        public async Task<User?> GetUser(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            var session = await proxy.GetSessionById(peer.SessionId, cancellationToken);
            return session?.User;
        }

        public Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return proxy.GetUsers(userIds, cancellationToken);
        }

        public Task<bool> IsAuthenticated(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            return proxy.IsAuthenticated(peer.SessionId, cancellationToken);
        }

        public Task KickUser(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken)
        {
            return proxy.KickUser(userIds, reason, cancellationToken);
        }

        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            return proxy.Query(query, take, skip, cancellationToken);
        }

        public async Task UpdateSessionData(SessionId sessionId, string key, byte[] data, CancellationToken cancellationToken)
        {
            await using var rq = proxy.UpdateSessionData(sessionId, key, cancellationToken);

            await rq.Writer.WriteAsync(data, cancellationToken);
            rq.Writer.Complete();
            rq.Reader.Complete();
            rq.Send();
        }

        public async Task UpdateSessionData<T>(SessionId sessionId, string key, T data, CancellationToken cancellationToken)
        {
            await using var rq = proxy.UpdateSessionData(sessionId, key, cancellationToken);

            rq.Writer.WriteObject(data, _clusterSerializer);
            rq.Writer.Complete();
            rq.Reader.Complete();
            rq.Send();
        }

        public Task UpdateUserData<T>(IScenePeerClient peer, T data, CancellationToken cancellationToken)
        {
            return proxy.UpdateUserData(peer.SessionId, JObject.FromObject(data!), cancellationToken);


        }

        public async Task<IEnumerable<SessionId>> GetPeers(string userId, CancellationToken cancellationToken)
        {
           return await proxy.GetPeers(userId, cancellationToken);
         

        }

        public Task<IEnumerable<Session>> GetSessionsByUserId(string userId, CancellationToken cancellationToken)
        {
            return proxy.GetSessionsByUserId(userId, cancellationToken);
        }


        public IRemotePipe SendRequest(string operationName, string senderUserId, string recipientUserId, CancellationToken cancellationToken)
        {

            return proxy.SendRequest(operationName, senderUserId, recipientUserId, cancellationToken);
        }

        public Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
             => UserSessions.SendRequestImpl<TReturn, TArg>(this, _clientSerializer, _clusterSerializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        public Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg1, TArg2>(string operationName, string senderUserId, string recipientUserId, TArg1 arg1, TArg2 arg2, CancellationToken cancellationToken)
            => UserSessions.SendRequestImpl<TReturn, TArg1, TArg2>(this,_clientSerializer, _clusterSerializer, operationName, senderUserId, recipientUserId, arg1, arg2, cancellationToken);

        public Task<SendRequestResult> SendRequest<TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
            => UserSessions.SendRequestImpl<TArg>(this, _clientSerializer, _clusterSerializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        public Task<string?> UpdateUserHandle(string userId, string newHandle, CancellationToken cancellationToken)
        {
            return proxy.UpdateUserHandle(userId, newHandle, cancellationToken);
        }

        public IAsyncEnumerable<Session> GetSessionsAsync(CancellationToken cancellationToken)
        {
            return proxy.GetSessionsAsync(cancellationToken);
        }

        public Task UpdateUserOptionsAsync(string userId, string key, JObject value, CancellationToken cancellationToken)
        {
            return proxy.UpdateUserOptionsAsync(userId, key, value, cancellationToken);
        }

        public Task<Dictionary<string, UserSessionInfos>> GetDetailedUserInformationByIdentityAsync(string platform, IEnumerable<string> ids,CancellationToken cancellationToken)
        {
            return proxy.GetDetailedUserInformationByIdentity(platform, ids,cancellationToken);
        }

        public Task<Dictionary<PlatformId, UserSessionInfos>> GetDetailedUserInformationAsync(IEnumerable<PlatformId> platformIds, CancellationToken cancellationToken)
        {
            return proxy.GetDetailedUserInformationAsync(platformIds,cancellationToken);
        }

        public Task<IEnumerable<Session>> GetSessions(PlatformId userId, CancellationToken cancellationToken)
        {
            return proxy.GetSessionsByPlatformId(userId, cancellationToken);
        }

        
    }


}