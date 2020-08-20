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

using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using System.Linq;
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

        public async Task GetProfiles(ProfileCtx ctx)
        {
            if (!ctx.DisplayOptions.ContainsKey("steam"))
            {
                return;
            }

            var users = (await Task.WhenAll(ctx.Users.Select(id => _users.GetUser(id))))
                .Where(u => u.UserData.ContainsKey("steamid"))
                .ToList();

            if (users.Count == 0)
            {
                return;
            }

            if (ctx.DisplayOptions["steam"] == "details")
            {
                var steamProfiles = await _steam.GetPlayerSummaries(users.Select(u => (ulong)(u.UserData["steamid"] ?? 0)));

                foreach (var user in users)
                {
                    if (user != null)
                    {
                        ctx.UpdateProfileData(user.Id, "steam", j =>
                        {
                                var steamId = (ulong)(user.UserData["steamid"] ?? 0);
                                var steamProfile = steamProfiles[steamId];
                                if (steamProfile != null)
                                {
                                    j["steamid"] = steamId;
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
                        ctx.UpdateProfileData(user.Id, "steam", j =>
                        {
                            var steamId = (ulong)(user.UserData["steamid"] ?? 0);
                            j["steamid"] = steamId;
                            return j;
                        });
                    }
                }
            }
        }
    }
}
