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
using Stormancer.Management.Models;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Galaxy
{
    class GalaxyProfilePartBuilder : IProfilePartBuilder
    {
        private const string LOG_CATEGORY = "GalaxyProfilePartBuilder";
        private readonly IUserService _users;
        private readonly IGalaxyService _galaxyService;
        private readonly ILogger _logger;

        public GalaxyProfilePartBuilder(IUserService users, IGalaxyService galaxyService, ILogger logger)
        {
            _users = users;
            _galaxyService = galaxyService;
            _logger = logger;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken ct)
        {
            var hasProfilePartGalaxy = ctx.DisplayOptions.ContainsKey(GalaxyConstants.PLATFORM_NAME);
            var hasProfilePartUser = ctx.DisplayOptions.ContainsKey("user");

            if (!hasProfilePartGalaxy && !hasProfilePartUser)
            {
                return;
            }

            if (ctx.Users.Count() == 0)
            {
                return;
            }

            var allUsers = await _users.GetUsers(ctx.Users.ToArray(), ct);
            var users = allUsers
                .Where(kvp => kvp.Value != null && kvp.Value.UserData.ContainsKey(GalaxyConstants.PLATFORM_NAME))
                .Select(kvp => kvp.Value)
                .ToList();

            if (users.Count == 0)
            {
                return;
            }

            Dictionary<string, UserInfo> userInfos = new();
            if (hasProfilePartUser || hasProfilePartGalaxy)
            {
                List<string> galaxyIds = new();
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        var galaxyId = user.GetGalaxyId();

                        if (galaxyId != null)
                        {
                            galaxyIds.Add(galaxyId);
                        }
                    }
                }
                userInfos = await _galaxyService.GetUserInfos(galaxyIds);
            }

            if (hasProfilePartGalaxy)
            {
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        var galaxyId = user.GetGalaxyId();

                        if (!string.IsNullOrWhiteSpace(galaxyId))
                        {
                            ctx.UpdateProfileData(user.Id, GalaxyConstants.PLATFORM_NAME, data =>
                            {
                                data[GalaxyConstants.GALAXYID_CLAIMPATH] = galaxyId;

                                if (userInfos.TryGetValue(galaxyId, out var value))
                                {
                                    data[GalaxyConstants.USERNAME] = value.username;
                                }

                                return data;
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
                        var galaxyId = user.GetGalaxyId();
                        if (!string.IsNullOrWhiteSpace(galaxyId))
                        {
                            ctx.UpdateProfileData(user.Id, "user", data =>
                            {
                                if (!data.ContainsKey("platforms"))
                                {
                                    data["platforms"] = new JObject();
                                }
                                data["platforms"]![GalaxyConstants.PLATFORM_NAME] = new JObject();
                                data["platforms"]![GalaxyConstants.PLATFORM_NAME]![GalaxyConstants.GALAXYID_CLAIMPATH] = galaxyId;

                                if (user.LastPlatform == GalaxyConstants.PLATFORM_NAME)
                                {
                                    if (userInfos.TryGetValue(galaxyId, out var value))
                                    {
                                        data["pseudo"] = value.username;
                                    }
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
