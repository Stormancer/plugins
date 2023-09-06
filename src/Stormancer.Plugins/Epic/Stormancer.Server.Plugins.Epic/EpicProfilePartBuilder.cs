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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    class EpicProfilePartBuilder : IProfilePartBuilder
    {
        private const string LOG_CATEGORY = "EpicProfilePartBuilder";
        private readonly IUserService _users;
        private readonly IEpicService _epicService;
        private readonly ILogger _logger;

        public EpicProfilePartBuilder(IUserService users, IEpicService epicService, ILogger logger)
        {
            _users = users;
            _epicService = epicService;
            _logger = logger;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken ct)
        {
            var hasProfilePartEpic = ctx.DisplayOptions.ContainsKey(EpicConstants.PLATFORM_NAME);
            var hasProfilePartUser = ctx.DisplayOptions.ContainsKey("user");

            if (!hasProfilePartEpic && !hasProfilePartUser)
            {
                return;
            }

            if (ctx.Users.Count() == 0)
            {
                return;
            }

            var allUsers = await _users.GetUsers(ctx.Users.ToArray(), ct);
            var users = allUsers
                .Where(kvp => kvp.Value != null && kvp.Value.UserData.ContainsKey(EpicConstants.PLATFORM_NAME))
                .Select(kvp => kvp.Value)
                .ToList();

            if (users.Count == 0)
            {
                return;
            }

            Dictionary<string, Account> accounts = new();
            Dictionary<string, string?> productUserIds = new();
            if (hasProfilePartUser || hasProfilePartEpic)
            {
                List<string> accountIds = new();
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        var accountId = user.GetAccountId();

                        if (accountId != null)
                        {
                            accountIds.Add(accountId);
                        }
                    }
                }
                accounts = await _epicService.GetAccounts(accountIds);

                if (ctx.Origin != null && ctx.Origin.User != null)
                {
                    productUserIds = await _epicService.GetExternalAccounts(accountIds, "epicgames");
                }
            }

            if (hasProfilePartEpic)
            {
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        var accountId = user.GetAccountId();

                        if (!string.IsNullOrWhiteSpace(accountId))
                        {
                            ctx.UpdateProfileData(user.Id, EpicConstants.PLATFORM_NAME, data =>
                            {
                                data[EpicConstants.ACCOUNTID_CLAIMPATH] = accountId;

                                if (productUserIds.TryGetValue(accountId, out var productUserId) && !string.IsNullOrWhiteSpace(productUserId))
                                {
                                    data[EpicConstants.PRODUCTUSERID] = productUserId;
                                }

                                if (accounts.TryGetValue(accountId, out var account) && account != null && !string.IsNullOrWhiteSpace(account.DisplayName))
                                {
                                    data[EpicConstants.DISPLAYNAME] = account.DisplayName;
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
                        var accountId = user.GetAccountId();
                        if (!string.IsNullOrWhiteSpace(accountId))
                        {
                            ctx.UpdateProfileData(user.Id, "user", data =>
                            {
                                if (!data.ContainsKey("platforms"))
                                {
                                    data["platforms"] = new JObject();
                                }
                                data["platforms"]![EpicConstants.PLATFORM_NAME] = new JObject();
                                data["platforms"]![EpicConstants.PLATFORM_NAME]![EpicConstants.ACCOUNTID_CLAIMPATH] = accountId;

                                if (productUserIds.TryGetValue(accountId, out var productUserId) && !string.IsNullOrWhiteSpace(productUserId))
                                {
                                    data["platforms"]![EpicConstants.PLATFORM_NAME]![EpicConstants.PRODUCTUSERID] = productUserId;
                                }

                                if (user.GetSelectedPlatformForPseudo() == EpicConstants.PLATFORM_NAME)
                                {
                                    if (accounts.TryGetValue(accountId, out var account) && account != null && !string.IsNullOrWhiteSpace(account.DisplayName))
                                    {
                                        data["pseudo"] = account.DisplayName;
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
