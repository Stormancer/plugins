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
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamFriendsEventHandler : IFriendsEventHandler
    {
        private IUserSessions _sessions;
        private readonly IUserService _users;
        private ISteamService _steamService;
        private readonly ISceneHost _scene;

        public SteamFriendsEventHandler(IUserSessions sessions, IUserService users, ISteamService steamService, ISceneHost scene)
        {
            _sessions = sessions;
            _users = users;
            _steamService = steamService;
            _scene = scene;
        }

        private class SteamFriendUser
        {
            public required SteamFriend SteamFriend { get; set; }
            public required UserSessionInfos User { get; set; }
            public SteamPlayerSummary? SteamPlayerSummary { get; set; } = null;
        }

        private FriendConnectionStatus SteamPersonaStateToStormancerFriendsStatus(int steamPersonaState)
        {
            switch (steamPersonaState)
            {
                case 1: // Online
                case 5: // Looking to trade
                case 6: // Looking to play
                    return FriendConnectionStatus.Connected;
                case 2: // Busy
                case 3: // Away
                case 4: // Snooze
                    return FriendConnectionStatus.Away;
                case 0: // Offline
                default: // Default
                    return FriendConnectionStatus.Disconnected;
            }
        }

        public Task OnGetFriends(GetFriendsCtx getFriendsCtx)
        {
            return Task.CompletedTask;
        }

        Task IFriendsEventHandler.OnAddingFriend(Stormancer.Server.Plugins.Friends.AddingFriendCtx ctx)
        {
            foreach (var friend in ctx.Friends)
            {
                if(friend.userInfos?.User !=null && !friend.friend.UserIds.Any(p=>p.Platform == SteamConstants.PLATFORM_NAME) && friend.userInfos.User.TryGetSteamId(out var steamId))
                {
                    friend.friend.UserIds.Add(new PlatformId(SteamConstants.PLATFORM_NAME, steamId.ToString()));
                }
            }

            return Task.CompletedTask;
        }

        private FriendConnectionStatus GetConnectionStatus(SteamFriendUser friend)
        {
            if(friend.User.Sessions.Any())
            {
                return FriendConnectionStatus.Connected;
            }
            else
            {
                return FriendConnectionStatus.Disconnected;
            }
        }
    }
}
