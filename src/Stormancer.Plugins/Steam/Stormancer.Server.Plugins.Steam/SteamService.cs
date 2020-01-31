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
using Stormancer.Server.Plugins.Steam.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    public class SteamService : ISteamService
    {
        private const string ApiRoot = "https://partner.steam-api.com";
        private const string FallbackApiRoot = "https://api.steampowered.com";
        private const string FallbackApiRooWithIp = "https://208.64.202.87";

        private string _apiKey;
        private uint _appId;

        private bool _usemockup;

        private ILogger _logger;

        public static HttpClient client = new HttpClient();

        public SteamService(IConfiguration configuration, ILogger logger)
        {
            _logger = logger;

            var steamElement = configuration.Settings?.steam;

            ApplyConfig(steamElement);

            configuration.SettingsChanged += (sender, settings) => ApplyConfig(settings?.steam);
        }

        private void ApplyConfig(dynamic steamElement)
        {
            _apiKey = (string)steamElement?.apiKey;

            var dynamicAppId = steamElement?.appId;
            if (dynamicAppId != null)
            {
                _appId = (uint)dynamicAppId;
            }

            var dynamicUseMockup = steamElement?.usemockup;
            if (dynamicUseMockup != null)
            {
                _usemockup = (bool)dynamicUseMockup;
            }
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
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var steamResponse = JsonConvert.DeserializeObject<SteamAuthenticationResponse>(json);

                if (steamResponse.response.error != null)
                {
                    throw new Exception($"The Steam API failed to authenticate user ticket : {steamResponse.response.error.errorcode} : '{steamResponse.response.error.errordesc}'. AppId : {_appId}");
                }
                else
                {
                    return steamResponse.response.@params.steamid;
                }
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

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
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

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
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

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
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

        public async Task<SteamPlayerSummary> GetPlayerSummary(ulong steamId)
        {
            return (await GetPlayerSummaries(new[] { steamId }))?[steamId];
        }

        public async Task<IEnumerable<SteamFriend>> GetFriendList(ulong steamId)
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
                return steamResponse.friendslist.friends;
            }
        }

        public async Task<SteamCreateLobbyData> CreateLobby(string lobbyName, LobbyType lobbyType, int maxMembers, IEnumerable<ulong> steamIdInvitedMembers = null, Dictionary<string, string> lobbyMetadata = null)
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
            return json.response;
        }

        public async Task RemoveUserFromLobby(ulong steamIdToRemove, ulong steamIdLobby)
        {
            if (steamIdToRemove == 0)
            {
                throw new ArgumentException("value is not correct", nameof(steamIdToRemove));
            }

            if (steamIdLobby == 0)
            {
                throw new ArgumentException("value is not correct", nameof(steamIdLobby));
            }

            var requestUrl = "ILobbyMatchmakingService/RemoveUserFromLobby/v1/";

            var removeUserFromLobbyInputJson = new
            {
                appid = _appId,
                steamid_to_remove = steamIdToRemove,
                steamid_lobby = steamIdLobby,
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
            var formUrlEncodedContent = new FormUrlEncodedContent(body);
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
