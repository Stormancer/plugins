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
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Users;
using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Steam party controller
    /// </summary>
    public class SteamPartyController : ControllerBase
    {
        private IPartyService _partyService { get; set; }
        private IUserService _userService { get; set; }
        private ISteamService _steamService { get; set; }

        /// <summary>
        /// Steam party controller constructor
        /// </summary>
        /// <param name="partyService"></param>
        /// <param name="userService"></param>
        /// <param name="steamService"></param>
        public SteamPartyController(IPartyService partyService, IUserService userService, ISteamService steamService)
        {
            _partyService = partyService;
            _userService = userService;
            _steamService = steamService;
        }

        /// <summary>
        /// Create lobby metadata bearer token.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreatePartyDataBearerToken()
        {
            var leaderUserId = _partyService.Settings.PartyLeaderId;
            var leaderUser = await _userService.GetUser(leaderUserId);
            var leaderSteamId = leaderUser.GetSteamId() ?? 0;
            return await _steamService.CreatePartyDataBearerToken(_partyService.PartyId, leaderUserId, leaderSteamId);
        }
    }
}
