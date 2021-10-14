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
using System.Threading;
using System.Threading.Tasks;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.Friends
{
    class FriendsProxy : IFriendsService
    {

        private readonly FriendsS2SProxy proxy;

        public FriendsProxy(FriendsS2SProxy proxy)
        {


            this.proxy = proxy;
        }

        public Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends,CancellationToken cancellationToken)
        {
            return proxy.AddNonPersistedFriends(userId, friends, cancellationToken);
        }

        public Task<IEnumerable<Friend>> GetFriends(string userId, CancellationToken cancellationToken)
        {
            return proxy.GetFriends(userId,cancellationToken) ;
        }

        public Task Invite(User user, User friendId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> IsInFriendList(string userId, string friendId)
        {
            throw new NotSupportedException();
        }

        public Task ManageInvitation(User user, string senderId, bool accept, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task RemoveFriend(User user, string friendId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetStatus(User user, FriendListStatusConfig status, string customData, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task Unsubscribe(IScenePeerClient peer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
