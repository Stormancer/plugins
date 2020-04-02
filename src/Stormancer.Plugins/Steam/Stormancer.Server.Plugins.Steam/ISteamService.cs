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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Steam service.
    /// </summary>
    public interface ISteamService
    {
        // ISteamUserAuth

        /// <summary>
        /// Authenticate user ticket.
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        Task<ulong?> AuthenticateUserTicket(string ticket);

        // ISteamUser

        /// <summary>
        /// Get player summary.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        Task<SteamPlayerSummary?> GetPlayerSummary(ulong steamId);

        /// <summary>
        /// Get multiple player summaries.
        /// </summary>
        /// <param name="steamIds"></param>
        /// <returns></returns>
        Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds);

        /// <summary>
        /// Get steam player friends.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        Task<IEnumerable<SteamFriend>> GetFriendList(ulong steamId);

        // ILobbyMatchmakingService

        /// <summary>
        /// Create a lobby.
        /// </summary>
        /// <param name="lobbyName"></param>
        /// <param name="lobbyType"></param>
        /// <param name="maxMembers"></param>
        /// <param name="steamIdInvitedMembers"></param>
        /// <param name="lobbyMetadata"></param>
        /// <returns></returns>
        Task<SteamCreateLobbyData> CreateLobby(string lobbyName, LobbyType lobbyType, int maxMembers, IEnumerable<ulong>? steamIdInvitedMembers = null, Dictionary<string, string>? lobbyMetadata = null);

        /// <summary>
        /// Remove a Steam user from the reserved slots of a Steam Lobby.
        /// </summary>
        /// <param name="steamIdToRemove"></param>
        /// <param name="steamIDLobby"></param>
        /// <returns></returns>
        Task RemoveUserFromLobby(ulong steamIdToRemove, ulong steamIDLobby);

        // ICheatReportingService

        /// <summary>
        /// Open VAC session.
        /// </summary>
        /// <param name="steamId"></param>
        /// <returns></returns>
        Task<string> OpenVACSession(string steamId);

        /// <summary>
        /// Close VAC session.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task CloseVACSession(string steamId, string sessionId);

        /// <summary>
        /// Request VAC status for user.
        /// </summary>
        /// <param name="steamId"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task<bool> RequestVACStatusForUser(string steamId, string sessionId);

        /// <summary>
        /// Decode Lobby metadata bearer token.
        /// </summary>
        /// <param name="tokens"></param>
        /// <returns></returns>
        Task<Dictionary<string, PartyDataDto>> DecodePartyDataBearerTokens(Dictionary<string, string> tokens);

        /// <summary>
        /// Create lobby metadata bearer token.
        /// </summary>
        /// <returns></returns>
        Task<string> CreatePartyDataBearerToken(string partyId, string leaderUserId, ulong leaderSteamId);
    }
}
