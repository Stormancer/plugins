using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Auth result
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        /// Access token.
        /// </summary>
        public string access_token { get; set; } = null!;

        /// <summary>
        /// Expires in.
        /// </summary>
        public ulong expires_in { get; set; }

        /// <summary>
        /// Expires at.
        /// </summary>
        public string expires_at { get; set; } = null!;

        /// <summary>
        /// Account Id.
        /// </summary>
        public string? account_id { get; set; } = null!;

        /// <summary>
        /// Client Id.
        /// </summary>
        public string? client_id { get; set; }

        /// <summary>
        /// Application Id.
        /// </summary>
        public string application_id { get; set; } = null!;

        /// <summary>
        /// Token Id.
        /// </summary>
        public string token_id { get; set; } = null!;

        /// <summary>
        /// Refresh token.
        /// </summary>
        public string? refresh_token { get; set; }

        /// <summary>
        /// Refresh expires.
        /// </summary>
        public ulong? refresh_expires { get; set; }

        /// <summary>
        /// Refresh expires at.
        /// </summary>
        public string? refresh_expires_at { get; set; }
    }

    /// <summary>
    /// Auth result
    /// </summary>
    public class EOSAuthResult
    {
        /// <summary>
        /// Generated access token as a JSON Web Token (JWT) string. The access token describes the authenticated client and user.
        /// </summary>
        public string? access_token { get; set; }

        /// <summary>
        /// Always set to bearer.
        /// </summary>
        public string? token_type { get; set; }

        /// <summary>
        /// Token expiration time. Contains a NumericDate number value, describing the time point in seconds from the Unix epoch.
        /// </summary>
        public string? expires_at { get; set; }

        /// <summary>
        /// Token lifetime. Seconds since the issue time to when the token will expire.
        /// </summary>
        public ulong expires_in { get; set; }

        /// <summary>
        /// Arbitrary string value provided by the client in the token request. Used by clients for added security. When receiving an access token in HTTP response, the client can verify that the token response includes the expected nonce value.
        /// </summary>
        public string? nonce { get; set; }

        /// <summary>
        /// Your organization identifier.
        /// </summary>
        public string? organization_id { get; set; }

        /// <summary>
        /// Your product identifier.
        /// </summary>
        public string? product_id { get; set; }

        /// <summary>
        /// Your sandbox identifier.
        /// </summary>
        public string? sandbox_id { get; set; }

        /// <summary>
        /// Your deployment identifier.
        /// </summary>
        public string? deployment_id { get; set; }

        /// <summary>
        /// features.
        /// </summary>
        public IEnumerable<string>? features { get; set; }

        /// <summary>
        /// organization_user_id.
        /// </summary>
        public string? organization_user_id { get; set; }

        /// <summary>
        /// product_user_id.
        /// </summary>
        public string? product_user_id { get; set; }

        /// <summary>
        /// id_token.
        /// </summary>
        public string? id_token { get; set; }
    }

    /// <summary>
    /// ExternalAccountsResult.
    /// </summary>
    public class ExternalAccountsResult
    {
        /// <summary>
        /// Ids.
        /// </summary>
        public Dictionary<string, string>? ids { get; set; }
    }

    /// <summary>
    /// Epic Platform service
    /// </summary>
    public class EpicService : IEpicService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserSessions _userSessions;
        private readonly ISerializer _serializer;
        private readonly ILogger _logger;
        private readonly ISecretsStore _secretsStore;
        private static readonly MemoryCache<AuthResult> _accessTokenCache = new();
        private static readonly MemoryCache<Account> _accountsCache = new();
        private static readonly MemoryCache<string> _externalAccountsCache = new();

        /// <summary>
        /// Epic service constructor
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="userSessions"></param>
        /// <param name="serializer"></param>
        /// <param name="logger"></param>
        /// <param name="secretsStore"></param>
        public EpicService(IConfiguration configuration, IHttpClientFactory httpClientFactory, IUserSessions userSessions, ISerializer serializer, ILogger logger, ISecretsStore secretsStore)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _userSessions = userSessions;
            _serializer = serializer;
            _logger = logger;
            _secretsStore = secretsStore;
        }

        /// <summary>
        /// Is Epic the main auth of this session?
        /// </summary>
        /// <param name="session"></param>
        /// <returns>bool</returns>
        public bool IsEpicAccount(Session session)
        {
            return (session.platformId.Platform == EpicConstants.PLATFORM_NAME);
        }

        private EpicConfigurationSection GetConfig()
        {
            return _configuration.GetValue<EpicConfigurationSection>(EpicConstants.PLATFORM_NAME);
        }

        /// <summary>
        /// Get Epic accounts.
        /// </summary>
        /// <param name="accountIds">Epic accounts ids</param>
        /// <returns>Epic accounts</returns>
        public async Task<Dictionary<string, Account>> GetAccounts(IEnumerable<string> accountIds)
        {
            var tasks = _accountsCache.GetMany(accountIds, (accountIds) =>
            {
                var result = new Dictionary<string, Task<(Account? account, TimeSpan cacheDuration)>>();

                var batch = Chunk(accountIds, 50).SelectMany(chunk =>
                {
                    var t = GetAccountsImpl(chunk);
                    return chunk.Select(id => (id, t));
                });

                foreach (var (id, task) in batch)
                {
                    static async Task<(Account? account, TimeSpan cacheDuration)> GetResult(string accountId, Task<Dictionary<string, (Account? account, TimeSpan cacheDuration)>> resultsTask)
                    {
                        var results = await resultsTask;
                        return results[accountId];
                    }

                    result[id] = GetResult(id, task);
                }

                return result;
            });

            await Task.WhenAll(tasks.Values);
            return tasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result)!;
        }

        private async Task<Dictionary<string, (Account? account, TimeSpan cacheDuration)>> GetAccountsImpl(IEnumerable<string> accountIds)
        {
            var accountIdsCount = accountIds.Count();
            if (accountIdsCount < 1)
            {
                return new Dictionary<string, (Account?, TimeSpan)>();
            }
            else if (accountIdsCount > 50)
            {
                throw new ArgumentException("Too many accountIds");
            }

            var url = "https://api.epicgames.dev/epic/id/v1/accounts?accountId=";
            url += string.Join("&accountId=", accountIds);

            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());

            var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var accounts = await response.Content.ReadFromJsonAsync<IEnumerable<Account>>();

                if (accounts == null)
                {
                    throw new InvalidOperationException("Accounts is null.");
                }

                return accounts.Where(account => account != null).ToDictionary(account => account.AccountId, account => (account, TimeSpan.FromSeconds(60)))!;
            }
            else
            {
                _logger.Log(LogLevel.Warn, "EpicService.GetAccountsImpl", "HTTP request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                throw new InvalidOperationException("HTTP request failed.");
            }
        }

        /// <summary>
        /// Get Epic accounts.
        /// </summary>
        /// <param name="requestorUserId"></param>
        /// <param name="externalAccountIds">Epic accounts ids</param>
        /// <param name="identityProviderId">identityProviderId</param>
        /// <param name="environment">environment</param>
        /// <returns>Epic accounts</returns>
        public async Task<Dictionary<string, string>> GetExternalAccounts(string requestorUserId, IEnumerable<string> externalAccountIds, string identityProviderId = "epicgames", string? environment = null)
        {
            var tasks = _externalAccountsCache.GetMany(externalAccountIds, (externalAccountIds) =>
            {
                var result = new Dictionary<string, Task<(string? productUserId, TimeSpan cacheDuration)>>();

                var batch = Chunk(externalAccountIds, 16).SelectMany(chunk =>
                {
                    var t = GetExternalAccountsImpl(requestorUserId, chunk, identityProviderId, environment);
                    return chunk.Select(id => (id, t));
                });

                foreach (var (id, task) in batch)
                {
                    static async Task<(string? productUserId, TimeSpan cacheDuration)> GetResult(string externalAccountId, Task<Dictionary<string, (string? productUserId, TimeSpan cacheDuration)>> resultsTask)
                    {
                        var results = await resultsTask;
                        return results[externalAccountId];
                    }

                    result[id] = GetResult(id, task);
                }

                return result;
            });

            await Task.WhenAll(tasks.Values);
            return tasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result)!;
        }

        private async Task<Dictionary<string, (string? productUserId, TimeSpan cacheDuration)>> GetExternalAccountsImpl(string requestorUserId, IEnumerable<string> accountIds, string identityProviderId = "epicgames", string? environment = null)
        {
            if (string.IsNullOrWhiteSpace(identityProviderId))
            {
                throw new ArgumentNullException("identityProviderId");
            }

            var accountIdsCount = accountIds.Count();
            if (accountIdsCount < 1)
            {
                return new Dictionary<string, (string?, TimeSpan)>();
            }
            else if (accountIdsCount > 16)
            {
                throw new ArgumentException("Too many accountIds");
            }

            var eosAccessToken = await GetEOSAccessToken(requestorUserId);

            if (string.IsNullOrWhiteSpace(eosAccessToken))
            {
                throw new InvalidOperationException("EosEpicAccessToken is invalid");
            }

            var url = "https://api.epicgames.dev/user/v1/accounts?accountId=";
            url += string.Join("&accountId=", accountIds);
            url += $"&identityProviderId={identityProviderId}";

            if (!string.IsNullOrWhiteSpace(environment))
            {
                url += $"&environment={environment}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", eosAccessToken);

            var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var accounts = await response.Content.ReadFromJsonAsync<ExternalAccountsResult>();

                if (accounts == null || accounts.ids == null)
                {
                    throw new InvalidOperationException("Accounts is null.");
                }

                return accounts.ids.ToDictionary(result => result.Key, result => (result.Value, TimeSpan.FromSeconds(60)))!;
            }
            else
            {
                _logger.Log(LogLevel.Warn, "EpicService.GetExternalAccountsImpl", "HTTP request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                throw new InvalidOperationException("HTTP request failed.");
            }
        }

        private async Task<string> GetAccessToken()
        {
            var authResult = await _accessTokenCache.Get("accessToken", async (_) =>
            {
                var deploymentIds = GetConfig().deploymentIds;
                if (deploymentIds == null || !deploymentIds.Any())
                {
                    throw new InvalidOperationException("DeploymentId is not set in config.");
                }

                var clientId = GetConfig().clientId;
                if (clientId == null)
                {
                    throw new InvalidOperationException("ClientId is not set in config.");
                }

                var clientSecretPath = GetConfig().clientSecret;
                if (clientSecretPath == null)
                {
                    throw new InvalidOperationException("ClientSecret is not set in config.");
                }
                var clientSecret = await GetClientSecret(clientSecretPath);

                var url = "https://api.epicgames.dev/epic/oauth/v1/token";
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "client_credentials" },
                        { "deployment_id", deploymentIds.First() },
                        { "scope", "basic_profile friends_list presence" },
                        { "client_id", clientId },
                        { "client_secret", clientSecret }
                    }),
                };

                var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

                var httpClient = new HttpClient();

                using var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var authResult = await response.Content.ReadFromJsonAsync<AuthResult>();

                    if (authResult == null)
                    {
                        throw new InvalidOperationException("AuthResult is null.");
                    }

                    var timespan = TimeSpan.FromSeconds(authResult.expires_in * 9 / 10);

                    return (authResult, timespan);
                }
                else
                {
                    _logger.Log(LogLevel.Warn, "EpicService.GetAccessToken", "Http request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                    throw new InvalidOperationException("HTTP request failed.");
                }
            });

            if (authResult == null)
            {
                throw new InvalidOperationException("AuthResult is null");
            }

            return authResult.access_token;
        }

        private async Task<string> GetEOSAccessToken(string requestorUserId)
        {
            var session = await _userSessions.GetSessionByUserId(requestorUserId, CancellationToken.None);
            if (session == null)
            {
                throw new InvalidOperationException("Session not found");
            }

            if (!session.SessionData.TryGetValue("EpicAccessToken", out var accessTokenBytes))
            {
                throw new InvalidOperationException("EpicAccessToken not found in SessionData");
            }

            var epicgamesAccessToken = Encoding.UTF8.GetString(accessTokenBytes);

            if (string.IsNullOrWhiteSpace(epicgamesAccessToken))
            {
                throw new InvalidOperationException("EpicAccessToken is invalid in SessionData");
            }

            if (string.IsNullOrWhiteSpace(epicgamesAccessToken))
            {
                throw new ArgumentNullException("externalAuthToken");
            }

            var deploymentIds = GetConfig().deploymentIds;
            if (deploymentIds == null || !deploymentIds.Any())
            {
                throw new InvalidOperationException("DeploymentId is not set in config.");
            }

            var clientId = GetConfig().clientId;
            if (clientId == null)
            {
                throw new InvalidOperationException("ClientId is not set in config.");
            }

            var clientSecretPath = GetConfig().clientSecret;
            if (clientSecretPath == null)
            {
                throw new InvalidOperationException("ClientSecret is not set in config.");
            }
            var clientSecret = await GetClientSecret(clientSecretPath);

            var url = "https://api.epicgames.dev/auth/v1/oauth/token";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "grant_type", "client_credentials" },
                        { "deployment_id", deploymentIds.First() },
                        { "nonce", "stormancer" },
                        { "client_id", clientId },
                        { "client_secret", clientSecret },
                        { "external_auth_token", epicgamesAccessToken },
                        { "external_auth_type", "epicgames_access_token" }
                    }),
            };

            var authHeaderValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            var httpClient = new HttpClient();

            using var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var authResult = await response.Content.ReadFromJsonAsync<EOSAuthResult>();

                if (authResult == null)
                {
                    throw new InvalidOperationException("EOSAuthResult is null.");
                }

                if (authResult.access_token == null)
                {
                    throw new InvalidOperationException("EOSAuthResult.access_token is null.");
                }

                return authResult.access_token;
            }
            else
            {
                _logger.Log(LogLevel.Warn, "EpicService.GetEOSAccessToken", "Http request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                throw new InvalidOperationException("HTTP request failed.");
            }
        }

        private async Task<string> GetClientSecret(string? clientSecretPath)
        {
            if (string.IsNullOrWhiteSpace(clientSecretPath))
            {
                throw new InvalidOperationException("Client secret store key is null.");
            }

            var secret = await _secretsStore.GetSecret(clientSecretPath);
            if (secret == null || secret.Value == null)
            {
                throw new InvalidOperationException($"Missing secret '{clientSecretPath}' in secrets store.");
            }

            return Encoding.UTF8.GetString(secret.Value) ?? "";
        }

        private static IEnumerable<T> Segment<T>(IEnumerator<T> iter, int size, out bool cont)
        {
            var ret = new List<T>();
            cont = true;
            bool hit = false;
            for (var i = 0; i < size; i++)
            {
                if (iter.MoveNext())
                {
                    hit = true;
                    ret.Add(iter.Current);
                }
                else
                {
                    cont = false;
                    break;
                }
            }

            return hit ? ret : null;
        }

        /// <summary>
        /// Breaks the collection into smaller chunks
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> collection, int size)
        {
            bool shouldContinue = collection != null && collection.Any();

            using (var iter = collection.GetEnumerator())
            {
                while (shouldContinue)
                {
                    //iteration of the enumerable is done in segment
                    var result = Segment(iter, size, out shouldContinue);

                    if (shouldContinue || result != null)
                        yield return result;

                    else yield break;
                }
            }
        }
    }
}
