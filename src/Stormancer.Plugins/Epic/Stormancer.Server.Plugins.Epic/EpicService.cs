﻿using Stormancer.Diagnostics;
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
        private readonly MemoryCache<AuthResult> _cache = new();

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
            var accountIdsCount = accountIds.Count();
            if (accountIdsCount < 1)
            {
                throw new ArgumentException("Missing accountIds");
            }
            else if (accountIdsCount > 50)
            {
                throw new ArgumentException("Too many accountIds");
            }

            var url = "https://api.epicgames.dev/epic/id/v1/accounts?";
            bool first = true;
            foreach (var accountId in accountIds)
            {
                if (!first)
                {
                    url += '&';
                }
                url += $"accountId={accountId}";
                first = false;
            }

            using var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                Content = new FormUrlEncodedContent(new Dictionary<string, string>()),
                RequestUri = new Uri(url)
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

                return accounts.ToDictionary(v => v.AccountId, v => v);
            }
            else
            {
                _logger.Log(LogLevel.Warn, "EpicService.GetAccounts", "Http request failed.", new { StatusCode = response.StatusCode, ResponseContent = response.Content });
                throw new InvalidOperationException("");
            }
        }

        private async Task<string> GetAccessToken()
        {
            var authResult = await _cache.Get("accessToken", async (_) =>
            {
                var deploymentId = GetConfig().deploymentId;
                if (deploymentId == null)
                {
                    throw new InvalidOperationException("CeploymentId is not set in config.");
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
                        { "deployment_id", deploymentId },
                        { "scope", "basic_profile friends_list presence" },
                        { "client_id", clientId },
                        { "client_secret", clientSecret }
                    }),
                };

                //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessToken());

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
                    throw new InvalidOperationException("");
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
                throw new InvalidOperationException("Client secret store key is null");
            }

            var secret = await _secretsStore.GetSecret(clientSecretPath);
            if (secret == null || secret.Value == null)
            {
                throw new InvalidOperationException($"Missing secret '{clientSecretPath}' in secrets store");
            }

            return Encoding.UTF8.GetString(secret.Value) ?? "";
        }

    }
}