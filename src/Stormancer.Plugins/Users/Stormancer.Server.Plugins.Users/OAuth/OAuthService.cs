using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Stormancer.Server.Plugins.Users.OAuth.JWKManager;

namespace Stormancer.Server.Plugins.Users.OAuth
{
    /// <summary>
    /// Provides methods to manage the app Json web keys.
    /// </summary>
    public class JWKManager : IConfigurationChangedEventHandler
    {
        private readonly ConfigurationMonitor<OAuthConfigurationSection> _configuration;
        private readonly ISecretsStore _secrets;

        internal JWKManager(ConfigurationMonitor<OAuthConfigurationSection> configuration, ISecretsStore secrets)
        {
            _configuration = configuration;
            _secrets = secrets;
        }

        /// <summary>
        /// Gets the current <see cref="KeysSet"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Task<KeysSet> GetPrivateKeys()
        {
            lock (_syncRoot)
            {
                if (_currentKeySets != null)
                {
                    return Task.FromResult(_currentKeySets);
                }

                if (_currentGetKeySetsRequest == null)
                {
                    _currentGetKeySetsRequest = LoadKeysFromStorage();
                }

                return _currentGetKeySetsRequest;
            }

        }

        /// <summary>
        /// Gets the active key.
        /// </summary>
        /// <returns></returns>
        public async Task<OAuthJsonKey?> GetActiveKey()
        {
            var keys = await GetPrivateKeys();

            // latest key is the active key.
            return keys.Keys.OrderByDescending(key => key.CreatedOn).FirstOrDefault();


        }

        /// <summary>
        /// Creates a new private key in the JSon Web Key format.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="keySize"></param>
        /// <returns></returns>
        public JsonWebKey CreatePrivateKey(string id,int keySize)
        {
            RSA rsa = RSA.Create(keySize);
            var p = rsa.ExportParameters(true);

            var kid = id;

            RsaSecurityKey publicAndPrivateKey1 = new(rsa.ExportParameters(true))
            {
                KeyId = kid
            };

            return JsonWebKeyConverter.ConvertFromRSASecurityKey(publicAndPrivateKey1);

        }

        private async Task<KeysSet> LoadKeysFromStorage()
        {
            var path = _configuration.Value.KeysPath;
            if (path == null)
            {
                return new KeysSet { Keys = new List<OAuthJsonKey>() };
            }

            var keysBlob = (await _secrets.GetSecret(path))?.Value;
            if (keysBlob == null)
            {
                throw new InvalidOperationException("private keys blob not found.");
            }
            try
            {
                var keysSet = System.Text.Json.JsonSerializer.Deserialize<KeysSet>(System.Text.Encoding.UTF8.GetString(keysBlob)) ?? new KeysSet { Keys = new List<OAuthJsonKey>() };

                lock (_syncRoot)
                {
                    _currentKeySets = keysSet;
                    _currentGetKeySetsRequest = null;
                }
            }
            catch (Exception)
            {
                lock (_syncRoot)
                {
                    _currentKeySets = new KeysSet { Keys = new List<OAuthJsonKey>() };
                    _currentGetKeySetsRequest = null;
                }
            }
            return _currentKeySets;
        }

        /// <summary>
        /// Adds a private key to the current key set.
        /// </summary>
        /// <param name="jwk"></param>
        public void AddPrivateKey(JsonWebKey jwk)
        {
            lock (_syncRoot)
            {
                if (_currentKeySets == null)
                {
                    throw new InvalidOperationException("keys set not loaded.");
                }

                _currentKeySets.Keys.Add(new OAuthJsonKey { CreatedOn =  DateTime.UtcNow, Key = jwk });
            }
        }

        /// <summary>
        /// Removes a private key from the current key set.
        /// </summary>
        /// <param name="keyId"></param>
        public void RemovePrivateKey(string keyId)
        {
            lock(_syncRoot)
            {
                if(_currentKeySets == null)
                {
                    throw new InvalidOperationException("keys set not loaded.");
                }

                _currentKeySets.Keys.RemoveAll(key => key.Key.KeyId == keyId);
            }
        }

        /// <summary>
        /// Saves the current keys set to storage.
        /// </summary>
        /// <returns></returns>
        public async Task SaveKeysToStorage()
        {
            var path = _configuration.Value.KeysPath;
            if (path == null)
            {
                return;
            }
            var json = System.Text.Json.JsonSerializer.Serialize(_currentKeySets);

            await _secrets.SetSecret(path, Encoding.UTF8.GetBytes(json));
        }

        void IConfigurationChangedEventHandler.OnConfigurationChanged()
        {
            lock (_syncRoot)
            {
                if (_currentGetKeySetsRequest == null)
                {
                    _currentGetKeySetsRequest = LoadKeysFromStorage();
                }
            }
        }

        private KeysSet? _currentKeySets;
        private object _syncRoot = new();
        private Task<KeysSet>? _currentGetKeySetsRequest;
    }

    /// <summary>
    /// Proviates OAuth related services.
    /// </summary>
    public class OAuthService
    {
        private readonly ConfigurationMonitor<OAuthConfigurationSection> _configuration;
        private readonly JWKManager _jwks;

        /// <summary>
        /// Creates a new object.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="jwks"></param>
        public OAuthService(ConfigurationMonitor<OAuthConfigurationSection> configuration, JWKManager jwks)
        {
            _configuration = configuration;
            _jwks = jwks;

        }

        /// <summary>
        /// Creates a JWK token signed with the active key.
        /// </summary>
        /// <param name="audience"></param>
        /// <param name="claims"></param>
        /// <returns></returns>
        public async Task<string> CreateToken(string audience, Dictionary<string, object> claims)
        {
            var config = _configuration.Value;
            if (config.KeysPath == null || config.Issuer == null || !config.ValidAudiences.Any())
            {
                throw new InvalidOperationException("Invalid 'users.oAuth' configuration section.");
            }
            var key = await _jwks.GetActiveKey();

            if (!config.ValidAudiences.Contains(audience))
            {
                throw new InvalidOperationException($"Invalid audience '{audience}'.");
            }
            if (config.Issuer is null)
            {
                throw new InvalidOperationException($"`auth.oauth.issuer` is not defined.");
            }

            var jwk = key?.Key;

            if (jwk == null)
            {
                throw new InvalidOperationException($"Active key not found.");
            }

            JsonWebTokenHandler tokenHandler = new();

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Issuer = config.Issuer,
                Audience = audience,
                Claims = claims,
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(GetRSAParametersFromJWK(jwk)) { KeyId = jwk.KeyId }, SecurityAlgorithms.RsaSha256)

            };
            return tokenHandler.CreateToken(tokenDescriptor);

        }




        /// <summary>
        /// Gets the JSON Web Key Set for this server.
        /// </summary>
        /// <returns></returns>
        public async Task<JsonWebKeySet> GetKeySet()
        {


            var privateKeys = await _jwks.GetPrivateKeys();

            var result = new JsonWebKeySet();

            foreach (var privateKey in privateKeys.Keys)
            {
                result.Keys.Add(GetPublicFromPrivateKey(privateKey.Key));
            }

            return result;
        }



        static JsonWebKey GetPublicFromPrivateKey(JsonWebKey jwk)
        {
            return new JsonWebKey { KeyId = jwk.KeyId, E = jwk.E, N = jwk.N, Alg = jwk.Alg, Kty = jwk.Kty, Use = jwk.Use };
        }


        static RSAParameters GetRSAParametersFromJWK(JsonWebKey jwk)
        {
            RSAParameters key = new RSAParameters();
            key.Exponent = Base64UrlEncoder.DecodeBytes(jwk.E);
            key.Modulus = Base64UrlEncoder.DecodeBytes(jwk.N);

            if (!string.IsNullOrEmpty(jwk.D))
            {
                key.D = Base64UrlEncoder.DecodeBytes(jwk.D);
                key.DP = Base64UrlEncoder.DecodeBytes(jwk.DP);
                key.DQ = Base64UrlEncoder.DecodeBytes(jwk.DQ);
                key.P = Base64UrlEncoder.DecodeBytes(jwk.P);
                key.Q = Base64UrlEncoder.DecodeBytes(jwk.Q);
                key.InverseQ = Base64UrlEncoder.DecodeBytes(jwk.QI);
            }

            return key;
        }

        public async Task<TokenValidationResult> ValidateToken(string jwt)
        {
            var config = _configuration.Value;
            var keySet = await GetKeySet();
            JsonWebTokenHandler tokenHandler = new();

            var token = tokenHandler.ReadJsonWebToken(jwt);
            var jwk = keySet.Keys.FirstOrDefault(jwk => jwk.KeyId == token.Kid);

            if (jwk == null)
            {
                return new TokenValidationResult { IsValid = false, Exception = new Exception($"Unknown kid: {token.Kid}") };
            }

            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = true,
                ValidAudiences = config.ValidAudiences,
                IssuerSigningKey = new RsaSecurityKey(GetRSAParametersFromJWK(jwk)) { KeyId = jwk.KeyId }
            };


            return await tokenHandler.ValidateTokenAsync(token, validationParameters);


        }
    }




    /// <summary>
    /// Key sets stored in the secret store.
    /// </summary>
    public class KeysSet
    {
        /// <summary>
        /// List of keys.
        /// </summary>
        public required List<OAuthJsonKey> Keys { get; set; }
    }

    /// <summary>
    /// Represents a key stored in the secret store.
    /// </summary>
    public class OAuthJsonKey
    {
        /// <summary>
        /// The date the key was created on.
        /// </summary>
        public required DateTime CreatedOn { get; set; }

        /// <summary>
        /// A key in the JWK format.
        /// </summary>
        public required JsonWebKey Key { get; set; }
    }

}
