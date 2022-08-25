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
            var accountTasks = _accountsCache.GetMany(accountIds, (accountIds2) =>
            {
                async Task<Dictionary<string, (Account?, TimeSpan)>> GetAccountsImpl(IEnumerable<string> accountIds)
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

                        var accountsResult = new Dictionary<string, (Account?, TimeSpan)>();

                        foreach (var accountId in accountIds2)
                        {
                            var account = accounts.FirstOrDefault(i => i.AccountId == accountId);
                            accountsResult[accountId] = (account, TimeSpan.FromSeconds(60));
                        }

                        return accountsResult;
                    }
                    else
                    {
                        _logger.Log(LogLevel.Warn, "EpicService.GetAccounts", "HTTP request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                        throw new InvalidOperationException("HTTP request failed.");
                    }
                }

                var result = new Dictionary<string, Task<(Account?, TimeSpan)>>();
                var t = GetAccountsImpl(accountIds2);
                foreach (var accountId in accountIds2)
                {
                    result[accountId] = t.ContinueWith(t => t.Result[accountId]);
                }
                return result;
            });

            await Task.WhenAll(accountTasks.Values);
            return accountTasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result)!;
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
                    _logger.Log(LogLevel.Warn, "EpicService.GetAccounts", "Http request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                    throw new InvalidOperationException("HTTP request failed.");
                }
            });

            if (authResult == null)
            {
                throw new InvalidOperationException("AuthResult is null");
            }

            return authResult.access_token;
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

    }
}
