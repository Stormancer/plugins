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
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    //TODO: update to support distributed scene
    class FriendsRepository
    {
        private struct UserContainer
        {
            public string sessionId;
            public FriendListConfigRecord config;
        }
        //[key=>{cid,config}]
        private readonly ConcurrentDictionary<string, UserContainer> _peers = new ConcurrentDictionary<string, UserContainer>();

        //[cId=>key]
        private readonly ConcurrentDictionary<string, string> _keys = new ConcurrentDictionary<string, string>();

        public FriendsRepository()
        {
        }
        public Task AddPeer(string key, IScenePeerClient peer, FriendListConfigRecord statusConfig)
        {
            if(statusConfig == null)
            {
                throw new ArgumentNullException("statusConfig");
            }
            var v = new UserContainer { sessionId = peer.SessionId, config = statusConfig };
            _keys.AddOrUpdate(peer.SessionId, key, (k, old) => key);
            _peers.AddOrUpdate(key, v, (k, old) => v);
            return Task.FromResult(true);
        }

        public Task<FriendListConfigRecord> GetStatusConfig(string key)
        {
            UserContainer p;
            if (_peers.TryGetValue(key, out p))
            {
                return Task.FromResult(p.config);
            }
            else
            {
                return Task.FromResult(default(FriendListConfigRecord));
            }
        }

        public Task<FriendListConfigRecord> UpdateStatusConfig(string key, FriendListStatusConfig status, string customData)
        {
            UserContainer p;
            if (_peers.TryGetValue(key, out p))
            {
                p.config = new FriendListConfigRecord { Id = key, CustomData = customData, LastConnected = DateTime.UtcNow, Status = status };
            }
            return Task.FromResult(p.config);
        }

        public Task<Tuple<FriendListConfigRecord,string>> RemovePeer(string sessionId)
        {
            UserContainer p = default(UserContainer);
            string key;
            if (_keys.TryRemove(sessionId, out key))
            {
                if (_peers.TryRemove(key, out p))
                {
                    p.config.LastConnected = DateTime.UtcNow;
                    return Task.FromResult(Tuple.Create(p.config, key));
                }
            }
            return Task.FromResult(Tuple.Create<FriendListConfigRecord, string>(null, key));
        }
    }
}
