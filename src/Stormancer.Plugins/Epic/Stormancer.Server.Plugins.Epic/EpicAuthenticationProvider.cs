using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Represents the header of an access token.
    /// </summary>
    public class AccessTokenHeader
    {
        /// <summary>
        /// Indicates a signature algorithm.
        /// </summary>
        public string? alg { get; set; }

        /// <summary>
        /// The identifier for the public key used in the signature.
        /// </summary>
        public string? kid { get; set; }
    }

    /// <summary>
    /// Represents the payload of an access token.
    /// </summary>
    public class EpicTokenPlayload
    {
        /// <summary>
        /// The base URI of the Epic Games authentication server that issued the token.
        /// </summary>
        public string iss { get; set; } = null!;

        /// <summary>
        /// This claim will not be present when using the client_credentials grant type.
        /// </summary>
        public string? sub { get; set; }

        /// <summary>
        /// The client ID specified in the authorization request.
        /// </summary>
        public string aud { get; set; } = null!;

        /// <summary>
        /// The time the token was issued as a UNIX timestamp.
        /// </summary>
        public int iat { get; set; }

        /// <summary>
        /// Expiration time of the token as a UNIX timestamp.
        /// </summary>
        public int exp { get; set; }

        /// <summary>
        /// The unique identifier for this token.
        /// </summary>
        public string jti { get; set; } = null!;

        /// <summary>
        /// The type of token. This should always be epic_id. This replaces the version prefix that we have with EG1 tokens.
        /// </summary>
        public string t { get; set; } = null!;

        /// <summary>
        /// Space delimited list of scopes that were authorized by the user.
        /// </summary>
        public string scope { get; set; } = null!;

        /// <summary>
        /// The user's display name.
        /// </summary>
        public string dn { get; set; } = null!;

        /// <summary>
        /// The application ID specified in the authorization request.
        /// </summary>
        public string appid { get; set; } = null!;

        /// <summary>
        /// The product ID for the deployment that was specified in the token request.
        /// </summary>
        public string? pfpid { get; set; }

        /// <summary>
        /// The sandbox ID for the deployment that was specified in the token request.
        /// </summary>
        public string? pfsid { get; set; }

        /// <summary>
        /// The deployment ID that was specified in the token request.
        /// </summary>
        public string? pfdid { get; set; }
    }

    /// <summary>
    /// Epic configuration class
    /// </summary>
    public class EpicConfigurationSection
    {
        /// <summary>
        /// Allowed Product ids.
        /// </summary>
        public IEnumerable<string>? productIds { get; set; }

        /// <summary>
        /// Allowed Application ids.
        /// </summary>
        public IEnumerable<string>? applicationIds { get; set; }

        /// <summary>
        /// Allowed Deployment ids.
        /// </summary>
        public IEnumerable<string>? deploymentIds { get; set; }

        /// <summary>
        /// Allowed Sandbox ids.
        /// </summary>
        public IEnumerable<string>? sandboxIds { get; set; }

        /// <summary>
        /// Client id.
        /// </summary>
        public string? clientId { get; set; }

        /// <summary>
        /// Client secret.
        /// </summary>
        public string? clientSecret { get; set; }
    }

    /// <summary>
    /// Json Web Token key
    /// </summary>
    public class JwtKey
    {
        /// <summary>
        /// The key type. Fixed to RSA.
        /// </summary>
        public string? kty { get; set; }

        /// <summary>
        /// The use of the key. Fixed to sig.
        /// </summary>
        public string? use { get; set; }

        /// <summary>
        /// The signature algorithm. Fixed to RSA256.
        /// </summary>
        public string? alg { get; set; }

        /// <summary>
        /// The identifier for the key used in the signature.
        /// </summary>
        public string? kid { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string? usage { get; set; }

        /// <summary>
        /// The modulus value of the Base64-encoded RSA public key.
        /// </summary>
        public string? n { get; set; }

        /// <summary>
        /// The exponent value of the Base64-encoded RSA public key.
        /// </summary>
        public string? e { get; set; }

        /// <summary>
        /// The Base64-encoded certificate chain in X.509 format.
        /// </summary>
        public IEnumerable<string>? x5c { get; set; }
    }

    /// <summary>
    /// Key collection response
    /// </summary>
    public class KeyCollectionResponse
    {
        /// <summary>
        /// Keys
        /// </summary>
        public IEnumerable<JwtKey> keys { get; set; } = null!;
    }

#pragma warning restore IDE1006 // Naming Styles

    class EpicAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IUserService _users;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUserSessions _userSessions;
        private readonly ISerializer _serializer;
        private readonly IEpicService _epicService;
        private MemoryCache<KeyCollectionResponse> _keysCache = new MemoryCache<KeyCollectionResponse>();

        public EpicAuthenticationProvider(IUserService users, ILogger logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, IEpicService epicService, IUserSessions userSessions, ISerializer serializer)
        {
            _users = users;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _userSessions = userSessions;
            _serializer = serializer;
            _epicService = epicService;
        }

        public string Type => EpicConstants.PLATFORM_NAME;

        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.epic", "enabled");
        }

        private EpicConfigurationSection GetConfig()
        {
            return _configuration.GetValue<EpicConfigurationSection>("epic");
        }

        private async Task<JwtKey> GetKey(AccessTokenHeader headers)
        {
            var kid = headers.kid;

            if (kid == null)
            {
                throw new ArgumentException("kid null.");
            }

            var jwkEndpoint = "https://api.epicgames.dev/epic/oauth/v1/.well-known/jwks.json";

            var keyCollection = await _keysCache.Get(jwkEndpoint, GetKeyCollection, TimeSpan.FromMinutes(60));

            var key = keyCollection?.keys?.FirstOrDefault(key => key.kid == kid);

            if (key == null)
            {
                throw new InvalidOperationException($"Cannot decode access token: Key {kid} not found at {jwkEndpoint}.");
            }

            return key;
        }

        private async Task<KeyCollectionResponse?> GetKeyCollection(string jwk)
        {
            var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync(jwk);

            response.EnsureSuccessStatusCode();

            return JsonConvert.DeserializeObject<KeyCollectionResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var authParams = authenticationCtx.Parameters;

            var pId = new PlatformId { Platform = EpicConstants.PLATFORM_NAME };

            var config = GetConfig();

            _logger.Log(LogLevel.Trace, "authenticator.epic", "Epic auth request received", authParams);

            if (config.productIds == null || !config.productIds.Any())
            {
                return AuthenticationResult.CreateFailure($"Missing ProductIds in server config.", pId, authParams);
            }

            if (config.applicationIds == null || !config.applicationIds.Any())
            {
                return AuthenticationResult.CreateFailure($"Missing ApplicationIds in server config.", pId, authParams);
            }

            if (config.sandboxIds == null || !config.sandboxIds.Any())
            {
                return AuthenticationResult.CreateFailure($"Missing SandboxIds in server config.", pId, authParams);
            }

            if (config.deploymentIds == null || !config.deploymentIds.Any())
            {
                return AuthenticationResult.CreateFailure($"Missing DeploymentIds in server config.", pId, authParams);
            }

            if (!authParams.TryGetValue(EpicConstants.ACCESSTOKEN_CLAIMPATH, out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.Log(LogLevel.Trace, "authenticator.epic", "Epic auth request failed : accessToken is empty", authParams);
                return AuthenticationResult.CreateFailure($"{EpicConstants.ACCESSTOKEN_CLAIMPATH} must not be empty.", pId, authParams);
            }

            var headers = Jose.JWT.Headers<AccessTokenHeader>(accessToken);

            if (headers.kid == null) // Key id;
            {
                return AuthenticationResult.CreateFailure($"Invalid token (1).", pId, authParams);
            }

            try
            {
                var key = await GetKey(headers);

                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.ImportParameters(new RSAParameters
                    {
                        Modulus = Jose.Base64Url.Decode(key.n),
                        Exponent = Jose.Base64Url.Decode(key.e)
                    });

                    var payload = Jose.JWT.Decode<EpicTokenPlayload>(accessToken, rsa);

                    if (payload == null)
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Can't decode payload.", new { });
                        return AuthenticationResult.CreateFailure($"Invalid token (2).", pId, authParams);
                    }

                    if (string.IsNullOrWhiteSpace(payload.pfpid) || !config.productIds.Contains(payload.pfpid))
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Invalid product id", new { TokenApplicationId = payload.appid, ConfigProductIds = config.productIds });
                        return AuthenticationResult.CreateFailure($"Invalid token (3).", pId, authParams);
                    }

                    if (string.IsNullOrWhiteSpace(payload.appid) || !config.applicationIds.Contains(payload.appid))
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Invalid application id", new { TokenApplicationId = payload.appid, ConfigApplicationIds = config.applicationIds });
                        return AuthenticationResult.CreateFailure($"Invalid token (4).", pId, authParams);
                    }

                    if (string.IsNullOrWhiteSpace(payload.pfsid) || !config.sandboxIds.Contains(payload.pfsid))
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Invalid sandbox id", new { TokenApplicationId = payload.appid, ConfigSandboxIds = config.sandboxIds });
                        return AuthenticationResult.CreateFailure($"Invalid token (5).", pId, authParams);
                    }

                    if (string.IsNullOrWhiteSpace(payload.pfdid) || !config.deploymentIds.Contains(payload.pfdid))
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Invalid deployment id", new { TokenApplicationId = payload.appid, ConfigDeploymentIds = config.deploymentIds });
                        return AuthenticationResult.CreateFailure($"Invalid token (6).", pId, authParams);
                    }

                    if (payload.sub == null)
                    {
                        _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Invalid Epic account id", new { AccountId = payload.sub });
                        return AuthenticationResult.CreateFailure($"Invalid token (7).", pId, authParams);
                    }

                    var accountId = payload.sub;
                    pId.PlatformUserId = accountId;

                    var user = await _users.GetUserByClaim(EpicConstants.PLATFORM_NAME, EpicConstants.ACCOUNTID_CLAIMPATH, accountId);
                    if (user == null)
                    {
                        var userId = Guid.NewGuid().ToString("N");

                        var epicUserData = new JObject();
                        epicUserData[EpicConstants.ACCOUNTID_CLAIMPATH] = accountId;

                        var userData = new JObject();
                        userData[EpicConstants.PLATFORM_NAME] = epicUserData;

                        user = await _users.CreateUser(userId, userData, EpicConstants.PLATFORM_NAME);
                        user = await _users.AddAuthentication(user, EpicConstants.PLATFORM_NAME, claim => claim[EpicConstants.ACCOUNTID_CLAIMPATH] = accountId, new Dictionary<string, string> { { EpicConstants.ACCOUNTID_CLAIMPATH, accountId } });
                    }
                    else
                    {
                        if (user.LastPlatform != EpicConstants.PLATFORM_NAME)
                        {
                            user.LastPlatform = EpicConstants.PLATFORM_NAME;
                            await _users.UpdateLastPlatform(user.Id, EpicConstants.PLATFORM_NAME);
                        }

                        bool updateUserData = false;

                        var epicUserData = user.UserData[EpicConstants.PLATFORM_NAME] ?? new JObject();

                        var userDataAccountId = epicUserData[EpicConstants.ACCOUNTID_CLAIMPATH];
                        if (userDataAccountId == null || userDataAccountId.ToString() != accountId)
                        {
                            epicUserData[EpicConstants.ACCOUNTID_CLAIMPATH] = accountId;
                            updateUserData = true;
                        }

                        if (updateUserData)
                        {
                            user.UserData[EpicConstants.PLATFORM_NAME] = epicUserData;
                            await _users.UpdateUserData(user.Id, user.UserData);
                        }
                    }

                    var authResult = AuthenticationResult.CreateSuccess(user, pId, authParams);
                    var memStream = new MemoryStream();
                    _serializer.Serialize(payload, memStream);
                    authResult.OnSessionUpdated += (SessionRecord sessionRecord) =>
                    {
                        sessionRecord.SessionData["EpicAccessTokenPayload"] = memStream.ToArray();
                    };
                    return authResult;
                }
            }
            catch (Exception exception)
            {
                _logger.Log(LogLevel.Error, "EpicAuthenticationProvider.Authenticate", "Failed to decode token or to create/update user", new { exception });
                return AuthenticationResult.CreateFailure("Invalid token (8).", pId, authParams);
            }
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }

        public Task Setup(Dictionary<string, string> parameters)
        {
            throw new NotSupportedException();
        }

        public async Task Unlink(User user)
        {
            if (user != null)
            {
                var onlineId = (string?)user.Auth[EpicConstants.PLATFORM_NAME]?[EpicConstants.ACCOUNTID_CLAIMPATH];
                if (onlineId == null)
                {
                    throw new ClientException($"authentication.unlink_failed?reason=not_linked&provider={EpicConstants.PLATFORM_NAME}");
                }
                await _users.RemoveAuthentication(user, EpicConstants.PLATFORM_NAME);
            }
        }
    }
}
