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

namespace Stormancer.Server.Plugins.Users.OAuth
{
    /// <summary>
    /// Proviates OAuth related services.
    /// </summary>
    public class OAuthService
    {
        private readonly ConfigurationMonitor<OAuthConfigurationSection> _configuration;
        private readonly ISecretsStore _secrets;

        private readonly MemoryCache<string, KeySets> _keysCache = new MemoryCache<string, KeySets>();

        /// <summary>
        /// Creates a new object.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="secrets"></param>
        public OAuthService(ConfigurationMonitor<OAuthConfigurationSection> configuration, ISecretsStore secrets)
        {
            _configuration = configuration;
            _secrets = secrets;
        }

        /// <summary>
        /// Creates a JWK token signed with the active key.
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public async Task<string> CreateToken(Session session, string audience)
        {
            var config = _configuration.Value;
            if (config.ActiveKeyId == null || config.Issuer == null || !config.ValidAudiences.Any())
            {
                throw new InvalidOperationException("Invalid 'users.oAuth' configuration section.");
            }
            var keys = await GetPrivateKeys();

            if(!config.ValidAudiences.Contains(audience))
            {
                throw new InvalidOperationException($"Invalid audience '{audience}'.");
            }

            var jwk = keys.Keys.FirstOrDefault(k => k.KeyId == config.ActiveKeyId);

            if(jwk == null)
            {
                throw new InvalidOperationException($"Active OAuth key {config.ActiveKeyId} not found.");
            }
            
            JsonWebTokenHandler tokenHandler = new();

            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Issuer = "mtg",
                Audience = "eos",
                Claims = new Dictionary<string, object> { { "stormancer:userId", "xxx" } },
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(GetRSAParametersFromJWK(jwk)), SecurityAlgorithms.RsaSsaPssSha256)

            };
            return tokenHandler.CreateToken(tokenDescriptor);
            
        }


        /// <summary>
        /// Creates a new key pair in the JSon Web Key format.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public JsonWebKey CreatePrivateKey(string id)
        {
            RSA rsa = RSA.Create(2048);
            var p = rsa.ExportParameters(true);

            var kid = "2026-10-06-rsa";

            RsaSecurityKey publicAndPrivateKey1 = new(rsa.ExportParameters(true))
            {
                KeyId = kid
            };

            return JsonWebKeyConverter.ConvertFromRSASecurityKey(publicAndPrivateKey1);

        }

        /// <summary>
        /// Gets the JSON Web Key Set for this server.
        /// </summary>
        /// <returns></returns>
        public async Task<JsonWebKeySet> GetKeySet()
        {


            var privateKeys = await GetPrivateKeys();

            var result = new JsonWebKeySet();

            foreach (var privateKey in privateKeys.Keys)
            {
                result.Keys.Add(GetPublicFromPrivateKey(privateKey));
            }

            return result;
        }

        private async Task<KeySets> GetPrivateKeys()
        {
            var path = _configuration.Value.KeysPath;
            if (path == null)
            {
                return new KeySets { Keys = new List<JsonWebKey>() };
            }

            var keySets = await _keysCache.Get(path, async (p) =>
            {
                var keysBlob = (await _secrets.GetSecret(path))?.Value;
                if (keysBlob == null)
                {
                    throw new InvalidOperationException("private keys blob not found.");
                }
                return (System.Text.Json.JsonSerializer.Deserialize<KeySets>(System.Text.Encoding.UTF8.GetString(keysBlob)), TimeSpan.FromMinutes(10));
            });

            return keySets ?? new KeySets { Keys = new List<JsonWebKey>() };
        }

        static JsonWebKey GetPublicFromPrivateKey(JsonWebKey jwk)
        {
            return new JsonWebKey { KeyId = jwk.KeyId, E = jwk.E, N = jwk.N };
        }

        static (RSAParameters publicKey, RSAParameters privateKey) GetRSAParametersPairFromPrivateJWK(JsonWebKey jwk)
        {
            RSAParameters publicKey = new RSAParameters();
            RSAParameters privateKey = new RSAParameters();


            // PUBLIC KEY

            publicKey.Exponent = Base64UrlEncoder.DecodeBytes(jwk.E);
            publicKey.Modulus = Base64UrlEncoder.DecodeBytes(jwk.N);

            // PRIVATE KEY

            privateKey.Exponent = Base64UrlEncoder.DecodeBytes(jwk.E);
            privateKey.Modulus = Base64UrlEncoder.DecodeBytes(jwk.N);
            privateKey.D = Base64UrlEncoder.DecodeBytes(jwk.D);
            privateKey.DP = Base64UrlEncoder.DecodeBytes(jwk.DP);
            privateKey.DQ = Base64UrlEncoder.DecodeBytes(jwk.DQ);
            privateKey.P = Base64UrlEncoder.DecodeBytes(jwk.P);
            privateKey.Q = Base64UrlEncoder.DecodeBytes(jwk.Q);
            privateKey.InverseQ = Base64UrlEncoder.DecodeBytes(jwk.QI);

            return (publicKey, privateKey);
        }

        static RSAParameters GetRSAParametersFromJWK(JsonWebKey jwk)
        {
            RSAParameters privateKey = new RSAParameters();
            privateKey.Exponent = Base64UrlEncoder.DecodeBytes(jwk.E);
            privateKey.Modulus = Base64UrlEncoder.DecodeBytes(jwk.N);
            privateKey.D = Base64UrlEncoder.DecodeBytes(jwk.D);
            privateKey.DP = Base64UrlEncoder.DecodeBytes(jwk.DP);
            privateKey.DQ = Base64UrlEncoder.DecodeBytes(jwk.DQ);
            privateKey.P = Base64UrlEncoder.DecodeBytes(jwk.P);
            privateKey.Q = Base64UrlEncoder.DecodeBytes(jwk.Q);
            privateKey.InverseQ = Base64UrlEncoder.DecodeBytes(jwk.QI);

            return privateKey;
        }
    }




    /// <summary>
    /// Key sets stored in the secret store.
    /// </summary>
    public class KeySets
    {
        public required List<JsonWebKey> Keys { get; set; }
    }
}
