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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Represents an error returned by the steam API.
    /// </summary>
    public class SteamException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SteamException()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SteamException(string? message) : base(message)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public SteamException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        protected SteamException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    internal class SteamService : ISteamService
    {
        private const string ApiRoot = "https://partner.steam-api.com";
        private const string FallbackApiRoot = "https://api.steampowered.com";
        private const string FallbackApiRooWithIp = "https://208.64.202.87";

        private bool _usemockup;
        private uint _appId;
        private string _apiKey = "";
        private string _lobbyMetadataBearerTokenKey = "";

        private ILogger _logger;
        private IUserSessions _userSessions;

        public static HttpClient client = new HttpClient();

        /// <summary>
        /// Steam service constructor.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="userSessions"></param>
        public SteamService(IConfiguration configuration, ILogger logger, IUserSessions userSessions)
        {
            _logger = logger;
            _userSessions = userSessions;

            ApplyConfig(configuration.Settings);
        }

        private void ApplyConfig(dynamic config)
        {
            _usemockup = (bool?)config?.steam?.usemockup ?? false;
            _appId = (uint?)config?.steam?.appId ?? (uint)0;
            _apiKey = (string?)config?.steam?.apiKey ?? "";
            _lobbyMetadataBearerTokenKey = (string?)config?.steam?.lobbyMetadataBearerTokenKey ?? "";
        }

        public async Task<ulong?> AuthenticateUserTicket(string ticket)
        {
            if (_usemockup)
            {
                return (ulong)ticket.GetHashCode();
            }

            const string AuthenticateUri = "ISteamUserAuth/AuthenticateUserTicket/v0001/";

            var querystring = $"?key={_apiKey}"
                + $"&appid={_appId}"
                + $"&ticket={ticket}";

            _logger.Log(LogLevel.Debug, $"authenticator.steam", $"AuthenticateUserTicket: {AuthenticateUri}  Query: {querystring}", new { });

            using (var response = await TryGetAsync(AuthenticateUri + querystring))
            {
                if (!response.IsSuccessStatusCode)
                {
                    var contentStr = await response.Content.ReadAsStringAsync();

                    _logger.Log(LogLevel.Error, "authenticator.steam", "The Steam API failed to authenticate user ticket. No success status code.", new { AuthenticateUri, StatusCode = $"{(int)response.StatusCode} {response.ReasonPhrase}", Response = contentStr });

                    throw new SteamException($"The Steam API failed to authenticate user ticket. No success status code.");
                }

                var json = await response.Content.ReadAsStringAsync();
                var steamResponse = JsonConvert.DeserializeObject<SteamAuthenticationResponse>(json);

                if (steamResponse.response == null)
                {
                    throw new SteamException($"The Steam API failed to authenticate user ticket. The response is null.'. AppId : {_appId}");
                }

                if (steamResponse.response.error != null)
                {
                    throw new SteamException($"The Steam API failed to authenticate user ticket : {steamResponse.response.error.errorcode} : '{steamResponse.response.error.errordesc}'. AppId : {_appId}");
                }

                if (steamResponse.response.@params == null)
                {
                    throw new SteamException($"The Steam API failed to authenticate user ticket. The response params is null.'. AppId : {_appId}");
                }

                return steamResponse.response.@params.steamid;
            }
        }

        public async Task<string> OpenVACSession(string steamId)
        {
            const string uri = "ICheatReportingService/StartSecureMultiplayerSession/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId }
            };
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p!)))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic j = JObject.Parse(json);
                    var success = (bool)j.response.success;
                    var sessionId = (string)j.response.session_id;
                    return sessionId;
                }
            }
        }

        public async Task CloseVACSession(string steamId, string sessionId)
        {
            const string uri = "ICheatReportingService/EndSecureMultiplayerSession/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId },
                {"session_id",sessionId }
            };

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p!)))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var j = JObject.Parse(json);

                }
            }
        }

        public async Task<bool> RequestVACStatusForUser(string steamId, string sessionId)
        {
            const string uri = "ICheatReportingService/RequestVacStatusForUser/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId },
                {"session_id",sessionId }
            };

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p!)))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic j = JObject.Parse(json);
                    var sessionVerified = (bool)j.response.session_verified;
                    var success = (bool)j.response.success;
                    return success && sessionVerified;
                }
            }
        }

        public async Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds)
        {
            if (_usemockup)
            {
                return steamIds.ToDictionary(id => id, id => new SteamPlayerSummary { personaname = "player" + id.ToString(), steamid = id });
            }

            const string GetPlayerSummariesUri = "ISteamUser/GetPlayerSummaries/V0002/";

            var steamIdsWithoutRepeat = steamIds.Distinct().ToList();
            Dictionary<ulong, SteamPlayerSummary> result = new Dictionary<ulong, SteamPlayerSummary>();

            for (var i = 0; i * 100 < steamIdsWithoutRepeat.Count; i++)
            {
                var querystring = $"?key={_apiKey}"
                    + $"&steamids={string.Join(",", steamIdsWithoutRepeat.Skip(100 * i).Take(100).Select(v => v.ToString()))}";

                using (var response = await TryGetAsync(GetPlayerSummariesUri + querystring))
                {

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var steamResponse = JsonConvert.DeserializeObject<SteamPlayerSummariesResponse>(json);

                    foreach (var summary in steamResponse.response.players)
                    {
                        result.Add(summary.steamid, summary);
                    }
                }
            }

            return result;
        }

        public async Task<SteamPlayerSummary?> GetPlayerSummary(ulong steamId)
        {
            return (await GetPlayerSummaries(new[] { steamId }))?[steamId];
        }

        public async Task<IEnumerable<SteamFriend>> GetFriendListFromWebApi(ulong steamId)
        {
            var requestUrl = "ISteamUser/GetFriendList/v1/";
            var querystring = $"?key={_apiKey}&steamid={steamId}&relationship=friend";
            var response = await TryGetAsync(requestUrl + querystring);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return await Task.FromResult(new List<SteamFriend>());
            }
            else
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var steamResponse = JsonConvert.DeserializeObject<SteamGetFriendsResponse>(json);

                if (steamResponse.friendslist == null || steamResponse.friendslist.friends == null)
                {
                    throw new Exception("GetFriendList failed: The Steam API response is null.");
                }

                return steamResponse.friendslist.friends;
            }
        }

        public async Task<IEnumerable<SteamFriend>> GetFriendListFromClient(string userId)
        {
            return await _userSessions.SendRequest<IEnumerable<SteamFriend>, bool>("Steam.GetFriends", "", userId, true, CancellationToken.None);
        }

        /// <summary>
        /// Create a Steam lobby
        /// </summary>
        /// <param name="lobbyName"></param>
        /// <param name="lobbyType"></param>
        /// <param name="maxMembers"></param>
        /// <param name="steamIdInvitedMembers"></param>
        /// <param name="lobbyMetadata"></param>
        /// <returns></returns>
        /// <remarks>metadata does not work</remarks>
        public async Task<SteamCreateLobbyData> CreateLobby(string lobbyName, LobbyType lobbyType, int maxMembers, IEnumerable<ulong>? steamIdInvitedMembers = null, Dictionary<string, string>? lobbyMetadata = null)
        {
            if (string.IsNullOrEmpty(lobbyName))
            {
                throw new ArgumentException("value is not correct", nameof(lobbyName));
            }

            if (maxMembers < 1 || maxMembers > 250)
            {
                throw new ArgumentException("value should be between 1 and 250 (included)", nameof(maxMembers));
            }

            var requestUrl = "ILobbyMatchmakingService/CreateLobby/v1/";

            var createLobbyInputJson = new
            {
                appid = _appId,
                lobby_name = lobbyName,
                lobby_type = (int)LobbyType.FriendsOnly,
                max_members = maxMembers,
                steamid_invited_members = /*steamIdInvitedMembers ?? */new List<ulong>(),
                lobby_metadata = lobbyMetadata != null ? lobbyMetadata.Select(kvp => new { key_name = kvp.Key, key_value = kvp.Value }) : null,
            };
            var input_json = JsonConvert.SerializeObject(createLobbyInputJson, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var body = new Dictionary<string, string>
            {
                { "key", _apiKey },
                { "input_json", input_json },
            };

            var response = await TryPostForServiceAsync(requestUrl, body);
            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.DeserializeObject<SteamCreateLobbyResponse>(jsonResponse);

            if (json.response == null)
            {
                throw new InvalidOperationException("Lobby creation failed (response is null).");
            }

            return json.response;
        }

        public async Task RemoveUserFromLobby(ulong steamIdToRemove, ulong steamIDLobby)
        {
            if (steamIdToRemove == 0)
            {
                throw new ArgumentException("value is not correct", nameof(steamIdToRemove));
            }

            if (steamIDLobby == 0)
            {
                throw new ArgumentException("value is not correct", nameof(steamIDLobby));
            }

            var requestUrl = "ILobbyMatchmakingService/RemoveUserFromLobby/v1/";

            var removeUserFromLobbyInputJson = new
            {
                appid = _appId,
                steamid_to_remove = steamIdToRemove,
                steamid_lobby = steamIDLobby,
            };
            var input_json = JsonConvert.SerializeObject(removeUserFromLobbyInputJson, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            var body = new Dictionary<string, string>
            {
                { "key", _apiKey },
                { "input_json", input_json },
            };

            var response = await TryPostForServiceAsync(requestUrl, body);
            response.EnsureSuccessStatusCode();
        }

        public Task<Dictionary<string, PartyDataDto>> DecodePartyDataBearerTokens(Dictionary<string, string> tokens)
        {
            return Task.FromResult(
                tokens.ToDictionary(kvp => kvp.Key, kvp => TokenGenerator.DecodeToken<PartyDataDto>(kvp.Value, _lobbyMetadataBearerTokenKey))
            );
        }

        public Task<string> CreatePartyDataBearerToken(string partyId, string leaderUserId, ulong leaderSteamId)
        {
            return Task.FromResult(
                TokenGenerator.CreateToken(new PartyDataDto { PartyId = partyId, LeaderUserId = leaderUserId, LeaderSteamId = leaderSteamId }, _lobbyMetadataBearerTokenKey)
            );
        }

        private async Task<HttpResponseMessage> TryGetAsync(string requestUrl)
        {
            try
            {
                return await client.GetAsync(new Uri(new Uri(ApiRoot), requestUrl));
            }
            catch (HttpRequestException)
            {
                try
                {
                    return await client.GetAsync(new Uri(new Uri(FallbackApiRoot), requestUrl));
                }
                catch (HttpRequestException)
                {
                    return await client.GetAsync(new Uri(new Uri(FallbackApiRooWithIp), requestUrl));
                }
            }
        }

        private async Task<HttpResponseMessage> TryPostAsync<T>(string requestUrl, T body)
        {
            var json = JsonConvert.SerializeObject(body, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                return await client.PostAsync(new Uri(new Uri(ApiRoot), requestUrl), stringContent);
            }
            catch (HttpRequestException)
            {
                try
                {
                    return await client.PostAsync(new Uri(new Uri(FallbackApiRoot), requestUrl), stringContent);
                }
                catch (HttpRequestException)
                {
                    return await client.PostAsync(new Uri(new Uri(FallbackApiRooWithIp), requestUrl), stringContent);
                }
            }
        }

        private async Task<HttpResponseMessage> TryPostForServiceAsync(string requestUrl, Dictionary<string, string> body)
        {
            var formUrlEncodedContent = new FormUrlEncodedContent(body!);
            try
            {
                return await client.PostAsync(new Uri(new Uri(ApiRoot), requestUrl), formUrlEncodedContent);
            }
            catch (HttpRequestException)
            {
                try
                {
                    return await client.PostAsync(new Uri(new Uri(FallbackApiRoot), requestUrl), formUrlEncodedContent);
                }
                catch (HttpRequestException)
                {
                    return await client.PostAsync(new Uri(new Uri(FallbackApiRooWithIp), requestUrl), formUrlEncodedContent);
                }
            }
        }
    }
}
