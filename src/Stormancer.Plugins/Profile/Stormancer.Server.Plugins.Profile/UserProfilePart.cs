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
using Stormancer.Server.Plugins.Users;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    class PseudoProfilePart : IProfilePartBuilder
    {
        private readonly IUserService _users;
        private readonly CrossplayService _crossplay;

        public PseudoProfilePart(IUserService users, CrossplayService crossplay)
        {
            _users = users;
            this._crossplay = crossplay;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken cancellationToken)
        {
            if (!ctx.DisplayOptions.ContainsKey("user"))
            {
                return;
            }

            var allUsers = await _users.GetUsers(ctx.Users, cancellationToken);
            var users = allUsers.Where(kvp => kvp.Value != null);

            foreach (var pair in users)
            {
                var userId = pair.Key;
                var user = pair.Value;

                ctx.UpdateProfileData(userId, "user", j =>
                {
                    if (user != null)
                    {
                        j["userId"] = user.Id;
                        j["lastPlatform"] = user.LastPlatform ?? "";

                        if (!j.ContainsKey("platforms"))
                        {
                            j["platforms"] = new JObject();
                        }
                        if(!j.ContainsKey("crossplay"))
                        {
                            j["crossplay"] = JObject.FromObject(new CrossplayUserOptions { Enabled = _crossplay.IsCrossplayEnabled(user) });
                        }

                        if (!j.ContainsKey("pseudo"))
                        {
                            if (user.Pseudonym != null)
                            {
                                j["pseudo"] = user.Pseudonym;
                            }
                            else if (user.UserData.ContainsKey("handle"))
                            {
                                j["pseudo"] = user.UserData["handle"];
                                j["platforms"]![DeviceIdentifierConstants.PROVIDER_NAME] = new JObject();
                                j["platforms"]![DeviceIdentifierConstants.PROVIDER_NAME]![DeviceIdentifierConstants.ClaimPath] = user.UserData["handle"];
                            }
                        }
                    }
                    return j;
                });
            }
        }
    }
}
