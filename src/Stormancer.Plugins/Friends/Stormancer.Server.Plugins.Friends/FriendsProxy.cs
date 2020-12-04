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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.Friends
{
    class FriendsProxy : IFriendsService
    {
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;
        private readonly IServiceLocator _locator;

        public FriendsProxy(ISceneHost scene, ISerializer serializer, IServiceLocator locator)
        {

            _scene = scene;
            _serializer = serializer;
            _locator = locator;
        }

        private async IAsyncEnumerable<Packet<IScenePeer>> FriendsRpc(string route, Action<Stream> writer)
        {
            var rpc = _scene.DependencyResolver.Resolve<RpcService>();
            await foreach(var packet in rpc.Rpc(route, new MatchSceneFilter(await _locator.GetSceneId("stormancer.plugins.friends", "")), writer, PacketPriority.MEDIUM_PRIORITY).ToAsyncEnumerable())
            {
                yield return packet;
            }
        }

        public async Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends)
        {
            await foreach(var packet in FriendsRpc("FriendsS2S." + nameof(AddNonPersistedFriends), s => {
                _serializer.Serialize(userId, s);
                _serializer.Serialize(friends, s);
            }))
            {
                using (packet) { }
            }

        }

        public Task Invite(User user, User friendId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsInFriendList(string userId, string friendId)
        {
            throw new NotImplementedException();
        }

        public Task ManageInvitation(User user, string senderId, bool accept)
        {
            throw new NotImplementedException();
        }

        public Task RemoveFriend(User user, string friendId)
        {
            throw new NotImplementedException();
        }

        public Task SetStatus(User user, FriendListStatusConfig status, string customData)
        {
            throw new NotImplementedException();
        }

        public Task Subscribe(IScenePeerClient peer)
        {
            throw new NotImplementedException();
        }

        public Task Unsubscribe(IScenePeerClient peer)
        {
            throw new NotImplementedException();
        }
    }
}
