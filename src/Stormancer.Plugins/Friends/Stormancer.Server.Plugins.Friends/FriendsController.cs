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
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
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
        
        public async Task InviteFriend(RequestContext<IScenePeerClient> ctx)
        {
            var friendId = ctx.ReadObject<string>();
            if (friendId == null)
            {
                throw new ClientException("friendId argument must be non null");
            }

            var friend = await _users.GetUser(friendId);

            if(friend == null)
            {
                throw new ClientException($"User '{friendId}' does not exist.");
            }
            var user = await _userSessions.GetUser(ctx.RemotePeer);

            if (user == null)
            {
                throw new ClientException("You must be connected to perform this operation.");
            }
            
            if(friend.Id == user.Id)
            {
                throw new ClientException("You cannot invite yourself as a friend.");
            }
                        
            await _friends.Invite(user, friend);
        }

        public async Task AcceptFriendInvitation(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer);
            if (user == null)
            {
                throw new ClientException("You must be connected to perform this operation.");
            }
            var senderId = ctx.ReadObject<string>();
            var accepts = ctx.ReadObject<bool>();

            await _friends.ManageInvitation(user, senderId, accepts);
        }
        
        public async Task RemoveFriend(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer);
            if (user == null)
            {
                throw new ClientException("You must be connected to perform this operation.");
            }

            var friendId = ctx.ReadObject<string>();
            
            await _friends.RemoveFriend(user, friendId);
        }

        public async Task SetStatus(RequestContext<IScenePeerClient> ctx)
        {
            var user = await _userSessions.GetUser(ctx.RemotePeer);

            if (user == null)
            {
                throw new ClientException("You must be connected to perform this operation.");
            }

            var status = ctx.ReadObject<FriendListStatusConfig>();

            var details = ctx.ReadObject<string>();

            await _friends.SetStatus(user, status, details);
        }
    }
}
