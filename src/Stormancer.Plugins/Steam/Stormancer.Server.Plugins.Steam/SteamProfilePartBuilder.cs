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
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    class SteamProfilePartBuilder : IProfilePartBuilder
    {
        private const string LOG_CATEGORY = "SteamProfilePartBuilder";
        private readonly IUserService _users;
        private readonly ISteamService _steam;
        private readonly ILogger _logger;

        public SteamProfilePartBuilder(IUserService users, ISteamService steam, ILogger logger)
        {
            _users = users;
            _steam = steam;
            _logger = logger;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken cancellationToken)
        {
            var hasProfilePartSteam = ctx.DisplayOptions.ContainsKey(SteamConstants.PLATFORM_NAME);
            var hasProfilePartUser = ctx.DisplayOptions.ContainsKey("user");

            if (!hasProfilePartSteam && !hasProfilePartUser)
            {
                return;
            }

            var allUsers = await _users.GetUsers(ctx.Users, cancellationToken);
            var users = allUsers
                .Where(kvp => kvp.Value?.UserData.ContainsKey(SteamConstants.STEAM_ID)??false)
                .Select(kvp => kvp.Value!)
                .ToList();

            if (users.Count == 0)
            {
                return;
            }
            var steamIds = users.Where(u => u.UserData.ContainsKey(SteamConstants.STEAM_ID)).ToDictionary(u=>u.Id,u => u.GetSteamId()??0);
            var steamProfiles = await _steam.GetPlayerSummaries(steamIds.Values);
            
            if (hasProfilePartSteam)
            {
                if (ctx.DisplayOptions[SteamConstants.PLATFORM_NAME] == "details")
                {
                    foreach (var user in users)
                    {
                        if (user != null && steamIds.ContainsKey(user.Id))
                        {
                            ctx.UpdateProfileData(user.Id, SteamConstants.PLATFORM_NAME, j =>
                            {
                                var steamId = (ulong?)user.UserData[SteamConstants.STEAM_ID] ?? 0UL;
                                var steamProfile = steamProfiles[steamId];
                                if (steamProfile != null)
                                {
                                    j["steamid"] = steamId.ToString();
                                    j["personaname"] = steamProfile.personaname;
                                    j["personastate"] = steamProfile.personastate;
                                    j["avatar"] = steamProfile.avatarfull;
                                    j["profileurl"] = steamProfile.profileurl;
                                }
                                return j;
                            });
                        }
                    }
                }
                else
                {
                    foreach (var user in users)
                    {
                        if (user != null)
                        {
                            ctx.UpdateProfileData(user.Id, SteamConstants.PLATFORM_NAME, j =>
                            {
                                j["steamid"] = (ulong?)user.UserData[SteamConstants.STEAM_ID] ?? 0UL;
                                return j;
                            });
                        }
                    }
                }
            }

            if (hasProfilePartUser)
            {
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        var steamId = user.GetSteamId();
                        if (steamId!=null)
                        {
                            ctx.UpdateProfileData(user.Id, "user", data =>
                            {
                                if (!data.ContainsKey("platforms"))
                                {
                                    data["platforms"] = new JObject();
                                }
                                data["platforms"]![SteamConstants.PLATFORM_NAME] = new JObject();
                                data["platforms"]![SteamConstants.PLATFORM_NAME]![SteamConstants.STEAM_ID] = steamId;
                                if(!data.ContainsKey("pseudo") && steamProfiles.TryGetValue(steamId.Value,out var steamProfile))
                                {
                                    data["pseudo"] = steamProfile.personaname;
                                }
                                return data;
                            });
                        }
                    }
                }
            }
        }
    }
}
