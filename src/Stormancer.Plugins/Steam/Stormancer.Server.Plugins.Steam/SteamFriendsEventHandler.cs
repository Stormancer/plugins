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

using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamFriendsEventHandler : IFriendsEventHandler
    {
        private IUserService _userService;
        private ISteamService _steamService;

        public SteamFriendsEventHandler(IUserService userService, ISteamService steamService)
        {
            _userService = userService;
            _steamService = steamService;
        }

        private class SteamFriendUser
        {
            public SteamFriend? SteamFriend { get; set; } = null;
            public User? User { get; set; } = null;
            public SteamPlayerSummary? SteamPlayerSummary { get; set; } = null;
        }

        private FriendStatus SteamPersonaStateToStormancerFriendsStatus(int steamPersonaState)
        {
            switch (steamPersonaState)
            {
                case 1: // Online
                case 5: // Looking to trade
                case 6: // Looking to play
                    return FriendStatus.Online;
                case 2: // Busy
                case 3: // Away
                case 4: // Snooze
                    return FriendStatus.Away;
                case 0: // Offline
                default: // Default
                    return FriendStatus.Disconnected;
            }
        }

        public async Task OnGetFriends(GetFriendsCtx getFriendsCtx)
        {
            if (getFriendsCtx.FromServer == false)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(getFriendsCtx.UserId))
            {
                throw new InvalidOperationException("Invalid UserId");
            }

            var steamFriends = await _steamService.GetFriendListFromClient(getFriendsCtx.UserId);

            if (!steamFriends.Any())
            {
                return;
            }

            // Get users from friends
            var users = await _userService.GetUsersByClaim(SteamConstants.PROVIDER_NAME, SteamConstants.ClaimPath, steamFriends.Select(steamFriend => steamFriend.steamid).ToArray());

            // Remove users not found or already present in context friendList
            var friendDatas = steamFriends
                .Select(steamFriend => new SteamFriendUser
                {
                    SteamFriend = steamFriend,
                    User = users[steamFriend.steamid] ?? null
                })
                .Where(friendData => friendData.User != null && !getFriendsCtx.Friends.Any(friend => friend.UserId == friendData.User.Id))
                .ToArray();

            if (!friendDatas.Any())
            {
                return;
            }

            // Get steam player summaries
            var steamIds = friendDatas.Select(data => data?.SteamFriend != null ? ulong.Parse(data.SteamFriend.steamid) : 0);
            var steamPlayerSummaries = await _steamService.GetPlayerSummaries(steamIds);

            // Fill data with steam player summaries
            foreach (var friendData in friendDatas)
            {
                friendData.SteamPlayerSummary = steamPlayerSummaries.FirstOrDefault(kvp => kvp.Value.steamid.ToString() == friendData.SteamFriend?.steamid).Value;
            }

            // Add steam friends to friends list
            foreach (var friendData in friendDatas)
            {
                if (friendData.User?.Id != null)
                {
                    getFriendsCtx.Friends.Add(new Friend
                    {
                        UserId = friendData.User.Id,
                        Status = SteamPersonaStateToStormancerFriendsStatus(friendData.SteamPlayerSummary?.personastate ?? -1),
                        LastConnected = DateTimeOffset.FromUnixTimeSeconds(friendData.SteamPlayerSummary?.lastlogoff ?? -1),
                        Tags = new List<string> { "steam" },
                        CustomData = new Dictionary<string, string> { { "Details", "steamFriend" }, { "SteamId", friendData.SteamFriend?.steamid ?? "" } }
                    });
                }
            }
        }
    }
}
