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
    internal class UserSessionImpl :IUserSessions
    {
        private readonly UserSessionProxy proxy;
        private readonly ISerializer serializer;
        private readonly ISceneHost scene;

        public UserSessionImpl(UserSessionProxy proxy, ISerializer serializer, ISceneHost scene)
        {
            this.proxy = proxy;
            this.serializer = serializer;
            this.scene = scene;
        }

        public Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken)
        {
            return proxy.GetAuthenticatedUsersCount(cancellationToken);
        }

        public Task<PlatformId> GetPlatformId(string userId, CancellationToken cancellationToken)
        {
            return proxy.GetPlatformId(userId, cancellationToken);
        }

        public Task<Session?> GetSession(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            return GetSessionById(peer.SessionId, cancellationToken);
        }

        public async Task<Session?> GetSession(PlatformId platformId, CancellationToken cancellationToken)
        {
            var r = await GetSessions(Enumerable.Repeat(platformId, 1), cancellationToken);
            return r.FirstOrDefault().Value;
        }

        public Task<Session?> GetSessionById(string sessionId, CancellationToken cancellationToken)
        {
            return proxy.GetSessionById(sessionId, cancellationToken);
        }

        public async Task<T?> GetSessionData<T>(string sessionId, string key, CancellationToken cancellationToken)
        {
            var buffer = await proxy.GetSessionData(sessionId, key, cancellationToken);
            if(buffer == null)
            {
                return default;
            }
            using var stream = new MemoryStream(buffer);
            return serializer.Deserialize<T>(stream);
        }

        public Task<byte[]?> GetSessionData(string sessionId, string key, CancellationToken cancellationToken)
        {
            return proxy.GetSessionData(sessionId, key, cancellationToken);
        }

        public Task<Dictionary<PlatformId, Session?>> GetSessions(IEnumerable<PlatformId> platformIds, CancellationToken cancellationToken)
        {
            return proxy.GetSessionsByPlatformIds(platformIds, cancellationToken);
        }

        public Task<Dictionary<string, Session?>> GetSessions(IEnumerable<string> sessionIds, CancellationToken cancellationToken)
        {
            return proxy.GetSessionsbySessionIds(sessionIds, cancellationToken);
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

        public Task KickUser(string userId, string reason, CancellationToken cancellationToken)
        {
            return proxy.KickUser(userId, reason, cancellationToken);
        }

        public Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken)
        {
            return proxy.Query(query, take, skip, cancellationToken);
        }

        public async Task UpdateSessionData(string sessionId, string key, byte[] data, CancellationToken cancellationToken)
        {
            await using var rq =  proxy.UpdateSessionData(sessionId, key,cancellationToken);

            await rq.Writer.WriteAsync(data, cancellationToken);
            rq.Writer.Complete();
            rq.Reader.Complete();
        }

        public async Task UpdateSessionData<T>(string sessionId, string key, T data, CancellationToken cancellationToken)
        {
            await using var rq = proxy.UpdateSessionData(sessionId, key, cancellationToken);

            await rq.Writer.WriteObject(data, serializer, cancellationToken);
            rq.Writer.Complete();
            rq.Reader.Complete();
        }

        public Task UpdateUserData<T>(IScenePeerClient peer, T data, CancellationToken cancellationToken)
        {
            return proxy.UpdateUserData(peer.SessionId, JObject.FromObject(data!), cancellationToken);

           
        }

        public async Task<IScenePeerClient?> GetPeer(string userId, CancellationToken cancellationToken)
        {
            var session = await GetSessionByUserId(userId, cancellationToken);
            if(session == null)
            {
                return default;
            }

            return scene.RemotePeers.FirstOrDefault(p=>p.SessionId == session.SessionId);

        }

        public Task<Session?> GetSessionByUserId(string userId, CancellationToken cancellationToken)
        {
            return proxy.GetSessionByUserId(userId, cancellationToken);
        }

        
        public IRemotePipe SendRequest(string operationName, string senderUserId, string recipientUserId, CancellationToken cancellationToken)
        {
          
            return proxy.SendRequest(operationName, senderUserId, recipientUserId, cancellationToken);
        }

        public Task<TReturn> SendRequest<TReturn, TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
             => UserSessions.SendRequestImpl<TReturn, TArg>(this, serializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        public Task<TReturn> SendRequest<TReturn, TArg1, TArg2>(string operationName, string senderUserId, string recipientUserId, TArg1 arg1, TArg2 arg2, CancellationToken cancellationToken)
            => UserSessions.SendRequestImpl<TReturn, TArg1, TArg2>(this, serializer, operationName, senderUserId, recipientUserId, arg1, arg2, cancellationToken);

        public Task SendRequest<TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken)
            => UserSessions.SendRequestImpl<TArg>(this, serializer, operationName, senderUserId, recipientUserId, arg, cancellationToken);


        public Task<string> UpdateUserHandle(string userId, string newHandle, bool appendHash, CancellationToken cancellationToken)
        {
            return proxy.UpdateUserHandle(userId, newHandle, appendHash, cancellationToken);
        }
    }

   
}