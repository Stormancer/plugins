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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Steam controller.
    /// </summary>
    public class SteamController : ControllerBase
    {
        private IUserService _userService { get; set; }
        private ISteamService _steamService { get; set; }

        /// <summary>
        /// Steam controller constructor.
        /// </summary>
        /// <param name="userService"></param>
        /// <param name="steamService"></param>
        public SteamController(IUserService userService, ISteamService steamService)
        {
            _userService = userService;
            _steamService = steamService;
        }

        /// <summary>
        /// Query stormancer user ids from steam ids.
        /// </summary>
        /// <param name="steamIds"></param>
        /// <returns>Map<steamId, userId></returns>
        /// <remarks>Obsolete: This api has some security issues</remarks>
        [Obsolete]
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<ulong, string>> QueryUserIds(IEnumerable<ulong> steamIds)
        {
            var users = await _userService.GetUsersByClaim2(SteamConstants.PROVIDER_NAME, SteamConstants.ClaimPath, steamIds.Select(steamId => steamId.ToString()).ToArray());
            return users.ToDictionary(kvp => ulong.Parse(kvp.Key), kvp => kvp.Value.Id);
        }

        /// <summary>
        /// Decode Lobby metadata bearer token.
        /// </summary>
        /// <param name="tokens">Tokens to decode</param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<IEnumerable<LobbyMetadataDto>> DecodePartyDataBearerTokens(IEnumerable<string> tokens)
        {
            return await _steamService.DecodePartyDataBearerTokens(tokens);
        }
    }

    /// <summary>
    /// Steam party controller
    /// </summary>
    public class SteamPartyController : ControllerBase
    {
        private IPartyService _partyService { get; set; }
        private IUserSessions _userSessions { get; set; }
        private ISteamService _steamService { get; set; }

        /// <summary>
        /// Steam party controller constructor
        /// </summary>
        /// <param name="partyService"></param>
        /// <param name="userSessions"></param>
        /// <param name="steamService"></param>
        public SteamPartyController(IPartyService partyService, IUserSessions userSessions, ISteamService steamService)
        {
            _partyService = partyService;
            _userSessions = userSessions;
            _steamService = steamService;
        }

        /// <summary>
        /// Create lobby metadata bearer token.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreatePartyDataBearerToken()
        {
            var user = await _userSessions.GetUser(Request.RemotePeer);
            var steamIdStr = (string?)user.Auth[SteamConstants.PROVIDER_NAME]?[SteamConstants.ClaimPath];
            ulong steamId;

            if (steamIdStr == null)
            {
                throw new Exception("SteamId is null");
            }

            try
            {
                steamId = ulong.Parse(steamIdStr);
            }
            catch (Exception)
            {
                throw new Exception("SteamId is invalid");
            }

            return await _steamService.CreatePartyDataBearerToken(steamId, user.Id, _partyService.PartyId);
        }
    }
}
