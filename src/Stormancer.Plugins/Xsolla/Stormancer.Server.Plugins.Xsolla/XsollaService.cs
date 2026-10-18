using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Stormancer.Server.Plugins.Xsolla
{
    /// <summary>
    /// An Xsolla user.
    /// </summary>
    public class XSollaUser
    {
        /// <summary>
        /// Gets the id of the user in XSolla.
        /// </summary>


        [JsonPropertyName("id")]
        public required string Id { get; init; }
        /// <summary>
        /// Gets the email of the user.
        /// </summary>
        [JsonPropertyName("email")]
        public required string Email { get; init; }

        /// <summary>
        /// Gets the external id of the user.
        /// </summary>

        [JsonPropertyName("external_id")]
        public string? ExternalId { get; init; }

    }
    /// <summary>
    /// Xsolla project id.
    /// </summary>
    public struct XsollaProjectId
    {
        /// <summary>
        /// Gets the value
        /// </summary>
        public required string Value { get; init; }
    }

    /// <summary>
    /// XSolla user Id.
    /// </summary>
    public struct XsollaUserId(string value)
    {
      
        /// <summary>
        /// Gets the value
        /// </summary>
        public string Value { get; } = value;
    }

    /// <summary>
    /// Response to a generate JWT request.
    /// </summary>
    public class XsollaGenerateServerJWTResponse
    {
        /// <summary>
        /// Gets the access token generated.
        /// </summary>
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        /// <summary>
        /// Gets the refresh token
        /// </summary>
        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        /// <summary>
        /// JWT expiration period in seconds. Default value is 3600 seconds.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public required int ExpiresIn { get; init; }

        /// <summary>
        /// Scope is a mechanism in OAuth 2.0 to limit an application’s access to a user’s account.
        /// </summary>
        [JsonPropertyName("scope")]
        public required string Scope { get; init; }

        /// <summary>
        /// JWT type with `bearer` value.
        /// </summary>
        [JsonPropertyName("token_type")]
        public required string TokenType { get; init; }
    }

    /// <summary>
    /// Response to Xsolla user auth request.
    /// </summary>
    public class XsollaAuthResponse
    {
        /// <summary>
        /// Xsolla Login user JWT.
        /// </summary>
        [JsonPropertyName("token")]
        public required string Token { get; init; }
    }

    /// <summary>
    /// XSolla services.
    /// </summary>
    public class XsollaService : IConfigurationChangedEventHandler
    {
        private static Uri LOGIN_API = new Uri("https://login.xsolla.com");
        private readonly ConfigurationMonitor<XsollaConfigurationSection> _configuration;
        private readonly ISecretsStore _secrets;

        private static MemoryCache<string, string> _tokenCache = new();

        /// <summary>
        /// Creates a new <see cref="XsollaService"/> object.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="secrets"></param>
        public XsollaService(ConfigurationMonitor<XsollaConfigurationSection> configuration, ISecretsStore secrets)
        {
            _configuration = configuration;
           
            _secrets = secrets;
        }

        private async Task<string> GetServerJWT()
        {
            var config = _configuration.Value;
            if (config.ProjectId == null)
            {
                throw new InvalidOperationException("Configuration key 'xsolla.projectId' not set.");
            }

            if (config.ClientId == null)
            {
                throw new InvalidOperationException("Configuration key 'xsolla.clientId' not set.");
            }

            if (config.ClientSecretPath == null)
            {
                throw new InvalidOperationException("Configuration key 'xsolla.clientSecretPath' not set.");
            }


            var token = await _tokenCache.Get(config.ClientId, async id =>
            {
                var secret = await _secrets.GetSecret(config.ClientSecretPath);
                if (secret == null || secret.Value == null)
                {
                    throw new InvalidOperationException($"Secret '{config.ClientSecretPath}' not found.");
                }
                var clientSecret = System.Text.Encoding.ASCII.GetString(secret.Value);
                var client = new HttpClient();
                var response = await client.PostAsync(new Uri(LOGIN_API, "api/oauth2/token"), new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = config.ClientId,
                    ["client_secret"] = clientSecret
                }));

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<XsollaGenerateServerJWTResponse>();
                if (result == null)
                {
                    throw new InvalidOperationException("Invalid response");
                }

                return (result.AccessToken, TimeSpan.FromSeconds(result.ExpiresIn * 0.9));

            });

            if (token == null)
            {
                throw new InvalidOperationException("Failed to create XSolla server token.");
            }

            return token;


        }

        /// <summary>
        /// Creates 
        /// </summary>
        /// <returns></returns>
        private async Task<HttpClient> CreateServerClient()
        {
            var token = await GetServerJWT();
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-SERVER-AUTHORIZATION", token);
            return client;
        }
        //public async Task<XSollaUser?> GetUser(XSollaUserId id)
        //{
        //    var client = CreateServerClient();
        //}

        /// <summary>
        /// Authenticates an user using their custom id.
        /// </summary>
        /// <param name="customId"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string?> AuthByCustomId(string customId)
        {
            var config = _configuration.Value;
            if (config.ProjectId == null)
            {
                throw new InvalidOperationException("Configuration key 'xsolla.projectId' not set.");
            }

            var client = await CreateServerClient();
            var response = await client.PostAsJsonAsync(new Uri(LOGIN_API, $"api/users/login/server_custom_id?projectId={config.ProjectId}"), new
            {
                server_custom_id = customId
            });

            if(response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<XsollaAuthResponse>();
                return result?.Token;
            }
            else
            {
                return null;
            }

        }

        /// <summary>
        /// Gets an user by its token.
        /// </summary>
        /// <param name="jwt"></param>
        /// <returns></returns>
        public async Task<XSollaUser?> GetUserByToken(string jwt)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
            var httpResponse = await client.GetAsync(new Uri(LOGIN_API, "api/users/me"));
           
            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadFromJsonAsync<XSollaUser>();

        }

        /// <summary>
        /// Links a user from your game to a user from your Login project via an external ID. 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="externalId"></param>
        /// <remarks>
        /// Documentation: https://developers.xsolla.com/api/login/account-linking/link-user-ids-via-external-id 
        /// 
        /// You will not be able to link the same external ID to different users and update the external ID of a user if they already have it.
        /// </remarks>
        /// <returns></returns>
        public async Task LinkUserWithExternalId(XsollaUserId userId, string externalId)
        {
            var client = await CreateServerClient();

            var httpResponse = await client.PostAsJsonAsync(new Uri(LOGIN_API, "api/users/account/link_external_id"), new { external_account_id = externalId, user_id = userId.Value });

            httpResponse.EnsureSuccessStatusCode();
        }
        void IConfigurationChangedEventHandler.OnConfigurationChanged()
        {
            if (_configuration.Value.ClientId != null)
            {
                _tokenCache.Remove(_configuration.Value.ClientId);
            }
        }

        
    }
}
