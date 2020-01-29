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
using Stormancer.Server.Plugins.Steam.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    public interface ISteamService
    {
        // ISteamUserAuth
        Task<ulong?> AuthenticateUserTicket(string ticket);

        // ISteamUser
        Task<SteamPlayerSummary> GetPlayerSummary(ulong steamId);
        Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds);
        Task<IEnumerable<SteamFriend>> GetFriendList(ulong steamId);

        // ILobbyMatchmakingService
        Task<SteamCreateLobbyData> CreateLobby(string lobbyName, LobbyType lobbyType, int maxMembers, IEnumerable<ulong> steamIdInvitedMembers = null, Dictionary<string, string> lobbyMetadata = null);
        Task RemoveUserFromLobby(ulong steamIdToRemove, ulong steamIdLobby);

        // ICheatReportingService
        Task<string> OpenVACSession(string steamId);
        Task CloseVACSession(string steamId, string sessionId);
        Task<bool> RequestVACStatusForUser(string steamId, string sessionId);
    }
}
