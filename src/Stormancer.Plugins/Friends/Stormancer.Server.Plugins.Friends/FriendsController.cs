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

using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    
    [Service(Named = false, ServiceType = FriendsConstants.SERVICE_ID)]
    class FriendsController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IUserSessions _userSessions;
        private readonly IFriendsService _friends;
        private readonly IUserService _users;

        public FriendsController(
            IUserSessions userSessions,
            ILogger logger,
            IFriendsService friends,
            IUserService users)
        {
            _logger = logger;
            _userSessions = userSessions;
            _friends = friends;
            _users = users;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task Subscribe(RequestContext<IScenePeerClient> ctx)
        {
           
            return _friends.Subscribe(ctx.RemotePeer, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task RefreshSubscription(RequestContext<IScenePeerClient> ctx)
        {
            
            return _friends.RefreshSubscription(ctx.RemotePeer, ctx.CancellationToken);
        }

        protected override Task OnDisconnected(DisconnectedArgs args) => _friends.Unsubscribe(args.Peer, System.Threading.CancellationToken.None);

        public async Task InviteFriend(RequestContext<IScenePeerClient> ctx)
        {
            var friendId = ctx.ReadObject<PlatformId>();
            
            var friend = await _users.GetUser(friendId);

            if (friend == null)
            {
                throw new ClientException($"User '{friendId}' does not exist.");
            }
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            if (friend.Id == user.Id)
            {
                throw new ClientException("You cannot invite yourself as a friend.");
            }

            await _friends.Invite(user, friend, ctx.CancellationToken);
        }

        public async Task AcceptFriendInvitation(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);
            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }
            var senderId = ctx.ReadObject<PlatformId>();
            var accepts = ctx.ReadObject<bool>();

            var sender = await _users.GetUser(senderId);
            if(sender == null)
            {
                throw new ClientException("UserNotFound");
            }
            await _friends.ManageInvitation(user, sender.Id, accepts, ctx.CancellationToken);
        }

        public async Task RemoveFriend(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);
            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var friendId = ctx.ReadObject<PlatformId>();

            var friend = await _users.GetUser(friendId);
            if(friend == null)
            {
                throw new ClientException("UserNotFound");
            }

            await _friends.RemoveFriend(user, friend, ctx.CancellationToken);
        }

        public async Task SetStatus(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var status = ctx.ReadObject<FriendListStatusConfig>();

            var details = ctx.ReadObject<string>();

            await _friends.SetStatus(user, status, details, ctx.CancellationToken);
        }

       

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Block(PlatformId userIdToBlock, string expirationDate, RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var target = await _users.GetUser(userIdToBlock);
            if (target == null)
            {
                throw new ClientException("UserNotFound");
            }

            await _friends.Block(user, target, !string.IsNullOrEmpty(expirationDate) ? DateTime.Parse(expirationDate) : DateTime.MaxValue, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Unblock(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var userIdToUnblock = ctx.ReadObject<PlatformId>();

            var target = await _users.GetUser(userIdToUnblock);
            if (target == null)
            {
                throw new ClientException("UserNotFound");
            }

            await _friends.Unblock(user, target, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<IEnumerable<string>> GetBlockedList(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            return await _friends.GetBlockedList(user.Id, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public,ApiType.Rpc)]
        public async Task<List<Friend>> Get(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if (user == null)
            {
                throw new ClientException("NotAuthenticated");
            }

            var result = new List<Friend>();

            foreach(var friend in await _friends.GetFriends(user.Id,ctx.CancellationToken))
            {
                result.Add(friend);
            }
            return result;
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
        public async Task Block(string userId, string userIdToBlock,DateTime expiration, CancellationToken cancellationToken)
        {
            var users = await _users.GetUsers(new[] { userId, userIdToBlock }, cancellationToken);
            if(users.TryGetValue(userId,out var sender) && sender is not null && users.TryGetValue(userIdToBlock, out var target) && target is not null)
            {
                await _friends.Block(sender,target, expiration, cancellationToken);
            }
           
           
        }

        [S2SApi]
        public async Task Unblock(string userId, string userIdToUnblock, CancellationToken cancellationToken)
        {
            var users = await _users.GetUsers(new[] { userId, userIdToUnblock }, cancellationToken);
            if (users.TryGetValue(userId, out var sender) && sender is not null && users.TryGetValue(userIdToUnblock, out var target) && target is not null)
            {
                await _friends.Unblock(sender, target, cancellationToken);
            }
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


        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task UpdateFriendList(Packet<IScenePeerClient> packet)
        {
            
            var updates = packet.ReadObject<IEnumerable<FriendListUpdateDto>>();

            var user = await _userSessions.GetUser(packet.Connection, CancellationToken.None);

            if (user != null)
            {
                await _friends.ProcessUpdates(user.Id, updates);
            }

          
        }

        [S2SApi]
        public Task UpdateFriendList(string friendListOwnerId, IEnumerable<FriendListUpdateDto> updates)
        {
         
            return _friends.ProcessUpdates(friendListOwnerId, updates);

         
        }

        [S2SApi]
        public Task<FriendConnectionStatus> GetStatus(PlatformId platformId, CancellationToken cancellationToken)
        {
            return _friends.GetStatusAsync(platformId, cancellationToken);
        }
    }
}
