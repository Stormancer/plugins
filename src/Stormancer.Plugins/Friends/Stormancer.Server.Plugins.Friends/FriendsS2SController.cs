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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    [Service(Named = false, ServiceType = "stormancer.plugins.friends")]
    public class FriendsS2SController : ControllerBase
    {
        private readonly IFriendsService _friends;

        public FriendsS2SController(IFriendsService friends)
        {
            _friends = friends;
        }

        [S2SApi]
        public Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends, CancellationToken cancellationToken)
        {
            return _friends.AddNonPersistedFriends(userId, friends, cancellationToken);
        }

        [S2SApi]
        public Task<IEnumerable<Friend>> GetFriends(string userId, CancellationToken cancellationToken)
        {
            return _friends.GetFriends(userId, cancellationToken);
        }

        [S2SApi]
        public Task<MemberDto?> GetRelationship(string userId, string targetUserId, CancellationToken cancellationToken)
        {
            return _friends.GetRelationship(userId, targetUserId, cancellationToken);
        }

        [S2SApi]
        public Task Block(string userId, string userIdToBlock, CancellationToken cancellationToken)
        {
            return _friends.Block(userId, userIdToBlock, cancellationToken);
        }

        [S2SApi]
        public Task Unblock(string userId, string userIdToUnblock, CancellationToken cancellationToken)
        {
            return _friends.Unblock(userId, userIdToUnblock, cancellationToken);
        }

        [S2SApi]
        public Task<Dictionary<string, IEnumerable<string>>> GetBlockedLists(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return _friends.GetBlockedLists(userIds, cancellationToken);
        }

        [S2SApi]
        public Task<IEnumerable<string>> GetBlockedList(string userId, CancellationToken cancellationToken)
        {
            return _friends.GetBlockedList(userId, cancellationToken);
        }
    }
}
