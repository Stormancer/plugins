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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{

    /// <summary>
    /// Provides API to get and manage friends.
    /// </summary>
    public interface IFriendsService
    {
        Task Invite(User user, User friendId, CancellationToken cancellationToken);

        Task<bool> IsInFriendList(string userId, string friendId);

        Task ManageInvitation(User user, string senderId, bool accept, CancellationToken cancellationToken);

        /// <summary>
        /// Removes an user from a friend list.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="friend"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RemoveFriend(User user, User friend, CancellationToken cancellationToken);

        Task Subscribe(IScenePeerClient peer, CancellationToken cancellationToken);

        Task RefreshSubscription(IScenePeerClient peer, CancellationToken cancellationToken);

        Task Unsubscribe(IScenePeerClient peer, CancellationToken cancellationToken);

        Task SetStatus(User user, FriendListStatusConfig status, string customData, CancellationToken cancellationToken);

        Task AddNonPersistedFriends(string userId, IEnumerable<Friend> friends, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the friend list of an user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<Friend>> GetFriends(string userId, CancellationToken cancellationToken);

        Task<MemberDto?> GetRelationship(string userId, string targetUserId, CancellationToken cancellationToken);

        /// <summary>
        /// Blocks an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="userToBlock"></param>
        /// <param name="expiration"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Block(User user, User userToBlock, DateTime expiration, CancellationToken cancellationToken);

        /// <summary>
        /// Unblocks an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="userToUnblock"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task Unblock(User user, User userToUnblock, CancellationToken cancellationToken);

        Task<Dictionary<string, IEnumerable<string>>> GetBlockedLists(IEnumerable<string> userIds, CancellationToken cancellationToken);

        Task<IEnumerable<string>> GetBlockedList(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Processes updates to a friend list.
        /// </summary>
        /// <remarks>
        /// Plugins can emit friend list updates either from the server or from clients.
        /// </remarks>
        /// <param name="updates"></param>
        Task ProcessUpdates(string userId, IEnumerable<FriendListUpdateDto> updates);

        /// <summary>
        /// Gets the status of an user in the friend system.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<FriendConnectionStatus> GetStatusAsync(PlatformId userId, CancellationToken cancellationToken);
    }
}
