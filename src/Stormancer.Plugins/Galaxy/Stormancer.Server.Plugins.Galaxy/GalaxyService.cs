using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using Stormancer.Diagnostics;
using System.Net.Http.Json;

namespace Stormancer.Server.Plugins.Galaxy
{
    /// <summary>
    /// Avatar info.
    /// </summary>
    public class AvatarInfo
    {
        /// <summary>
        /// Gog image id.
        /// </summary>
        public string gog_image_id { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string small { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string small_2x { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string medium { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string medium_2x { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string large { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string large_2x { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string sdk_img_32 { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string sdk_img_64 { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string sdk_img_184 { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string menu_small { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string menu_small_2 { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string menu_big { get; set; } = null!;

        /// <summary>
        /// Avatar url.
        /// </summary>
        public string menu_big_2 { get; set; } = null!;
    }

    /// <summary>
    /// User info.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// User id.
        /// </summary>
        public string id { get; set; } = null!;

        /// <summary>
        /// Username.
        /// </summary>
        public string username { get; set; } = null!;

        /// <summary>
        /// Created date.
        /// </summary>
        public string created_date { get; set; } = null!;

        /// <summary>
        /// Avatars.
        /// </summary>
        public AvatarInfo avatar { get; set; } = null!;

        /// <summary>
        /// Is employee.
        /// </summary>
        public bool is_employee { get; set; }

        /// <summary>
        /// Tags.
        /// </summary>
        public IEnumerable<string> tags { get; set; } = null!;
    }

    /// <summary>
    /// Galaxy Platform service
    /// </summary>
    public interface IGalaxyService
    {
        /// <summary>
        /// Is Galaxy the main auth of this session?
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool IsGalaxyAccount(Session session);

        /// <summary>
        /// Get Galaxy user infos.
        /// </summary>
        /// <param name="userIds"></param>
        /// <returns></returns>
        public Task<Dictionary<string, UserInfo>> GetUserInfos(IEnumerable<string> userIds);
    }

    /// <summary>
    /// Galaxy service.
    /// </summary>
    public class GalaxyService : IGalaxyService
    {
        private readonly ILogger _logger;
        private static readonly MemoryCache<string,UserInfo> _accountsCache = new();
        private const double _cacheTimeoutSeconds = 600;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public GalaxyService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Is Galaxy the main auth of this session?
        /// </summary>
        /// <param name="session"></param>
        /// <returns>bool</returns>
        public bool IsGalaxyAccount(Session session)
        {
            return (session.platformId.Platform == GalaxyConstants.PLATFORM_NAME);
        }

        /// <summary>
        /// Get Galaxy profiles.
        /// </summary>
        /// <param name="galaxyIds"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, UserInfo>> GetUserInfos(IEnumerable<string> galaxyIds)
        {
            var userInfosTasks = _accountsCache.GetMany(galaxyIds, (galaxyIds2) =>
            {
                var result = new Dictionary<string, Task<(UserInfo?, TimeSpan)>>();
                var t = GetUserInfosImpl(galaxyIds2);
                foreach (var galaxyId in galaxyIds2)
                {
                    result[galaxyId] = t.ContinueWith(t => t.Result[galaxyId]);
                }
                return result;
            });

            await Task.WhenAll(userInfosTasks.Values);
            return userInfosTasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result)!;
        }

        private async Task<Dictionary<string, (UserInfo?, TimeSpan)>> GetUserInfosImpl(IEnumerable<string> galaxyIds)
        {
            List<Task<UserInfo?>> tasks = new();
            foreach (var galaxyId in galaxyIds)
            {
                tasks.Add(GetUserInfoImpl(galaxyId));
            }
            var results = await Task.WhenAll(tasks);
            return results.Where(userInfo => userInfo != null).ToDictionary(userInfo => userInfo!.id, userInfo => (userInfo, TimeSpan.FromSeconds(_cacheTimeoutSeconds)));
        }

        private async Task<UserInfo?> GetUserInfoImpl(string galaxyId)
        {
            var url = $"https://users.gog.com/users/{galaxyId}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()),
            };

            var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserInfo>();
            }
            else
            {
                _logger.Log(LogLevel.Warn, "GalaxyService.GetUserInfoImpl", "HTTP request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                throw new InvalidOperationException("HTTP request failed.");
            }
        }
    }
}
