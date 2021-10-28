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
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    class PseudoProfilePart : IProfilePartBuilder
    {
        private readonly IUserService _users;
        private readonly IUserSessions _sessions;

        public PseudoProfilePart(IUserService users, IUserSessions sessions)
        {
            _users = users;
            _sessions = sessions;
        }

        public async Task GetProfiles(ProfileCtx ctx, CancellationToken cancellationToken)
        {
            if (!ctx.DisplayOptions.ContainsKey("user"))
            {
                return;
            }

            //TODO: Optimize.
            foreach (var id in ctx.Users)
            {
                // Prefer to retrieve the user directly from the session. If the user is offline, use the database.
                var session = await _sessions.GetSessionByUserId(id, cancellationToken);
                var user = session?.User ?? await _users.GetUser(id);
                ctx.UpdateProfileData(id, "user", j =>
                  {
                      if (user != null)
                      {
                          j["lastPlatform"] = user.LastPlatform ?? "";

                          if (!j.ContainsKey("platforms"))
                          {
                              j["platforms"] = new JObject();
                          }

                          if (!j.ContainsKey("handle") && user.UserData.ContainsKey("handle"))
                          {
                              j["userhandle"] = user.UserData["handle"];
                          }

                          if (!j.ContainsKey("pseudo") && user.UserData.ContainsKey("pseudo"))
                          {
                              j["pseudo"] = user.UserData["pseudo"];
                          }
                      }
                      return j;
                  });
            }
        }
    }
}
