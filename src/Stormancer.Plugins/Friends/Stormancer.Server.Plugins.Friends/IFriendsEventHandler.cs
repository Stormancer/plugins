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
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    /// <summary>
    /// Context object passed to <see cref="IFriendsEventHandler.OnGetFriends(GetFriendsCtx)"/>.
    /// </summary>
    public class GetFriendsCtx
    {
        internal GetFriendsCtx(IFriendsService friendsService, string userId, SessionId sessionId,List<Friend> friends, bool fromServer)
        {
            FriendsService = friendsService;
            UserId = userId;
            SessionId = sessionId;
            Friends = friends;
            FromServer = fromServer;
        }
        /// <summary>
        /// Gets the user id friends are requested for.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Gets the id of the session of the user requesting the friends.
        /// </summary>
        public SessionId SessionId { get; }

        /// <summary>
        /// Gets the current friends list.
        /// </summary>
        public List<Friend> Friends { get; }

        /// <summary>
        /// The friends request is from the server.
        /// </summary>
        public bool FromServer { get; }

        /// <summary>
        /// Gets the friends server that produced the event.
        /// </summary>
        public IFriendsService FriendsService { get; }
    }

    /// <summary>
    /// Context for <see cref="IFriendsEventHandler.OnAddingFriend"/>
    /// </summary>
    public class AddingFriendCtx
    {
        internal AddingFriendCtx(IFriendsService friendsService, string ownerId, IEnumerable<(UserSessionInfos? userInfos,Friend friend)> friends)
        {
            FriendsService = friendsService;
            FriendListOwnerId = ownerId;
            Friends = friends;
        }

        /// <summary>
        /// Gets the id of the owner of the friend list.
        /// </summary>
        public string FriendListOwnerId { get; }

        /// <summary>
        /// Gets the sender of the event.
        /// </summary>
        public IFriendsService FriendsService { get; }

        /// <summary>
        /// Gets the list of friends being added or updated in the friend list.
        /// </summary>
        public IEnumerable<(UserSessionInfos? userInfos, Friend friend)> Friends { get; }
    }

    /// <summary>
    /// Context for <see cref="IFriendsEventHandler.OnUpdatingStatus"/>
    /// </summary>
    public class UpdatingFriendStatusCtx
    {
        internal UpdatingFriendStatusCtx(IFriendsService friends, Guid ownerId, Friend friend, FriendConnectionStatus newStatus, User friendUser, Session friendSession)
        {
            Friends = friends;
            OwnerId = ownerId;
            Friend = friend;
            NewStatus = newStatus;
            FriendUser = friendUser;
            FriendSession = friendSession;
        }

        /// <summary>
        /// Gets the friends service
        /// </summary>
        public IFriendsService Friends { get; }

        /// <summary>
        /// Gets the owner of the list being updated.
        /// </summary>
        public Guid OwnerId { get; }

        /// <summary>
        /// Gets the friend object being updated.
        /// </summary>
        public Friend Friend { get; }

        /// <summary>
        /// Gets the updated status.
        /// </summary>
        public FriendConnectionStatus NewStatus { get; }

        /// <summary>
        /// Gets the user corresponding to the friend whose status is being updated.
        /// </summary>
        public User FriendUser { get; }

        /// <summary>
        /// Gets the user session corresponding to the friend whose status is being updated.
        /// </summary>
        public Session FriendSession { get; }
    }

    /// <summary>
    /// Friends extensibility points.
    /// </summary>
    public interface IFriendsEventHandler
    {
        /// <summary>
        /// Modifies the friend list.
        /// </summary>
        /// <param name="getFriendsCtx"></param>
        /// <returns></returns>
        Task OnGetFriends(GetFriendsCtx getFriendsCtx) => Task.CompletedTask;

        /// <summary>
        /// Called whenever a friend list is being updated with new friends.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnAddingFriend(AddingFriendCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Called whenever the connection status in the friends system of a friend object is updated.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnUpdatingStatus(UpdatingFriendStatusCtx ctx) => Task.CompletedTask;
    }
}
