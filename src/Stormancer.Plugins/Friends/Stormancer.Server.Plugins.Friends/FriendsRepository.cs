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
using Stormancer.Server.Plugins.Friends.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    //TODO: update to support distributed scene
    class FriendsRepository
    {
        private record struct UserContainer(SessionId sessionId, UserFriendListConfig config);

        //[userId=>{cid,config}]
        private readonly ConcurrentDictionary<Guid, UserContainer> _peers = new();

        //[cId=>userId]
        private readonly ConcurrentDictionary<SessionId, Guid> _keys = new();

        public FriendsRepository()
        {
        }
        public Task AddPeer(Guid key, IScenePeerClient peer, UserFriendListConfig statusConfig)
        {
            if (statusConfig == null)
            {
                throw new ArgumentNullException("statusConfig");
            }
            var v = new UserContainer { sessionId = peer.SessionId, config = statusConfig };
            _keys.AddOrUpdate(peer.SessionId, key, (k, old) => key);
            _peers.AddOrUpdate(key, v, (k, old) => v);
            return Task.FromResult(true);
        }

        public Task<UserFriendListConfig?> GetStatusConfig(Guid key)
        {
            UserContainer p;
            if (_peers.TryGetValue(key, out p))
            {
                return Task.FromResult<UserFriendListConfig?>(p.config);
            }
            else
            {
                return Task.FromResult(default(UserFriendListConfig));
            }
        }

        public Task<bool> UpdateStatusConfig(Guid key, UserFriendListConfig newConfig)
        {
            UserContainer p;
            if (_peers.TryGetValue(key, out p))
            {
                p.config = newConfig;
                return Task.FromResult(true);
            }
            else
            {
                return Task.FromResult(false);
            }

        }

        public Task<(UserFriendListConfig? config, Guid userId)> RemovePeer(SessionId sessionId)
        {
            UserContainer p = default(UserContainer);

            if (_keys.TryRemove(sessionId, out var key))
            {
                if (_peers.TryRemove(key, out p))
                {

                    return Task.FromResult(((UserFriendListConfig?)p.config, key));
                }
            }
            return Task.FromResult(((UserFriendListConfig?)null, key));
        }

        public IEnumerable<SessionId> GetSessionIds(IEnumerable<string> userIds)
        {
            foreach (var userId in userIds)
            {
                if(_peers.TryGetValue(Guid.Parse(userId),out var container))
                {
                    yield return container.sessionId;
                }
            }

        }
    }
}
