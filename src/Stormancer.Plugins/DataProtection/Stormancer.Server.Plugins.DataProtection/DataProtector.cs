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
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.DataProtection
{
    /// <summary>
    /// Configuration for the aes-gcm data protection policy.
    /// </summary>
    public class AesGcmProtectionConfig
    {
        /// <summary>
        /// Path to the key to use for data protection.
        /// </summary>
        public string? key { get; set; }

        /// <summary>
        /// Is the provider allowed to automatically generate the key.
        /// </summary>
        public bool createKeyIfNotExists { get; set; } = true;
        /// <summary>
        /// Nonce to use for data protection .
        /// </summary>
        /// <remarks>
        /// If a piece of encrypted cyphertext must always be the same for a given unencrypted value (for search purpose for instance)
        /// Set the nonce here. If not set, a new nonce will be generated for each cyphertext and searching won't be possible.
        /// </remarks>
        public string? nonce { get; set; }
    }
    class AesGcmProtectionProvider : IDataProtectionProvider
    {
        public static readonly int NonceBitSize = 96;
        public static readonly int MacBitSize = 96;
        public static readonly int KeyBitSize = 256;
        private readonly ISecretsStore secretsStore;

        //private const string key = "GZNFfCzreuClcc4NhotyJNB/VKSCbTUnFPwosFVyQOo=";
        //private const string nonce = "dkMRLIlJJAKZjQPx";

        public AesGcmProtectionProvider(ISecretsStore secretsStore)
        {
            this.secretsStore = secretsStore;
        }
        public string Id => "aes-gcm";

        public async Task<byte[]> Protect(byte[] value, string policy, JObject json)
        {
            var config = json.ToObject<AesGcmProtectionConfig>();
            if (config == null)
            {
                throw new InvalidOperationException($"Failed to load AesGcmProtection config from 'dataProtection.{policy}'");
            }
            if (string.IsNullOrEmpty(config.key))
            {
                throw new InvalidOperationException($"'key' configuration value missing or invalid in configuration object 'dataProtection.{policy}'. Should be an absolute file to the key file to use with the policy.");
            }
            if (!json.TryGetValue("nonce", out var nonceToken) || nonceToken == null || nonceToken.Type != JTokenType.String)
            {
                throw new InvalidOperationException($"'nonce' configuration value missing or invalid in configuration object 'dataProtection.{policy}'. Should be an absolute file to the nonce file to use with the policy.");
            }



           
            var key = await GetKey(config.key, config.createKeyIfNotExists);
            byte[] nonce;
            if (config.nonce != null)
            {
                nonce = Convert.FromBase64String(config.nonce);
            }
            else
            {
                var random = new Random();
                nonce = new byte[NonceBitSize / 8];
                random.NextBytes(nonce);
            }
            return SimpleEncrypt(value, key, nonce, null);

        }

        public async Task<byte[]> Unprotect(byte[] value, string policy, JObject json)
        {
            var config = json.ToObject<AesGcmProtectionConfig>();
            if (config == null)
            {
                throw new InvalidOperationException($"Failed to load AesGcmProtection config from 'dataProtection.{policy}'");
            }
            if (string.IsNullOrEmpty(config.key))
            {
                throw new InvalidOperationException($"'key' configuration value missing or invalid in configuration object 'dataProtection.{policy}'. Should be an absolute file to the key file to use with the policy.");
            }
            if (!json.TryGetValue("nonce", out var nonceToken) || nonceToken == null || nonceToken.Type != JTokenType.String)
            {
                throw new InvalidOperationException($"'nonce' configuration value missing or invalid in configuration object 'dataProtection.{policy}'. Should be an absolute file to the nonce file to use with the policy.");
            }




            var key = await GetKey(config.key, config.createKeyIfNotExists);
            var nonceLength = NonceBitSize / 8;
            return SimpleDecrypt(new Span<byte>(value, nonceLength, value.Length - nonceLength), new Span<byte>(value, 0, nonceLength), key);
        }

        private static MemoryCache<byte[]> _cache = new MemoryCache<byte[]>();
        private Task<byte[]?> GetKey(string keyPath, bool allowGenerate) => _cache.Get(keyPath,id=>GetKeyImpl(keyPath,allowGenerate));

        private async Task<(byte[]?, TimeSpan)> GetKeyImpl(string keyPath, bool allowGenerate)
        {
            var secret = await secretsStore.GetSecret(keyPath);
            if(secret.Value != null || !allowGenerate)
            {
                return (secret.Value, TimeSpan.FromSeconds(60));
            }   
            else
            {
                var key = new byte[32];

                RandomNumberGenerator.Fill(key);
                secret = await secretsStore.SetSecret(keyPath, key);

                return (secret.Value,TimeSpan.FromSeconds(60));
            }
            

        }

        /// <summary>
        /// Simple Encryption And Authentication (AES-GCM) of a UTF8 string.
        /// </summary>
        /// <param name="input">The secret message.</param>
        /// <param name="key">The key.</param>
        /// <param name="iv">The initialization vector.</param>
        /// <param name="nonSecretPayload">Optional non-secret payload.</param>
        /// <returns>Encrypted Message</returns>
        /// <remarks>
        /// Adds overhead of (Optional-Payload + BlockSize(16) + Message +  HMac-Tag(16)) * 1.33 Base64
        /// </remarks>
        private static byte[] SimpleEncrypt(byte[] input, byte[] key, byte[] iv, byte[]? nonSecretPayload = null)
        {
            //User Error Checks
            if (key == null || key.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KeyBitSize), "key");

            if (input == null)
                throw new ArgumentException("Secret Message Required!", "secretMessage");

            //Non-secret Payload Optional
            nonSecretPayload = nonSecretPayload ?? new byte[] { };

            //Using random nonce large enough not to repeat
            var nonce = iv;

            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), MacBitSize, nonce, nonSecretPayload);
            cipher.Init(true, parameters);

            //Generate Cipher Text With Auth Tag
            var cipherText = new byte[nonce.Length + cipher.GetOutputSize(input.Length)];

            nonce.CopyTo(cipherText, 0);
            var len = cipher.ProcessBytes(input, 0, input.Length, cipherText, nonce.Length);
            cipher.DoFinal(cipherText, len + nonce.Length);

            return cipherText;

        }

        /// <summary>
        /// Simple Decryption & Authentication (AES-GCM) of a UTF8 Message
        /// </summary>
        /// <param name="encryptedMessage">The encrypted message.</param>
        /// <param name="key">The key.</param>
        /// <param name="nonSecretPayloadLength">Length of the optional non-secret payload.</param>
        /// <returns>Decrypted Message</returns>
        private static byte[] SimpleDecrypt(Span<byte> input, Span<byte> nonce, byte[] key, byte[]? nonSecretPayload = null)
        {
            //User Error Checks
            if (key == null || key.Length != KeyBitSize / 8)
                throw new ArgumentException(String.Format("Key needs to be {0} bit!", KeyBitSize), "key");

            if (input == null)
                throw new ArgumentException("Encrypted Message Required!", "encryptedMessage");

            //Non-secret Payload Optional
            nonSecretPayload = nonSecretPayload ?? new byte[] { };



            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), MacBitSize, nonce.ToArray(), nonSecretPayload);
            cipher.Init(false, parameters);

            //Decrypt Cipher Text

            var plainText = new byte[cipher.GetOutputSize(input.Length)];


            var len = cipher.ProcessBytes(input.ToArray(), 0, input.Length, plainText, 0);
            cipher.DoFinal(plainText, len);


            return plainText;

        }
    }
    class DataProtector : IDataProtector
    {
        private readonly IEnumerable<IDataProtectionProvider> providers;
        private readonly IConfiguration config;

        public DataProtector(IEnumerable<IDataProtectionProvider> providers, IConfiguration config)
        {
            this.providers = providers;
            this.config = config;
        }


        public Task<byte[]> UnprotectBase64Url(string value, string? dataProtectionPolicy = null)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var segs = value.Split("|");
            if (segs.Length == 2)
            {
                dataProtectionPolicy = segs[0];
                value = segs[1];
            }

            if (dataProtectionPolicy == null)
            {
                dataProtectionPolicy = "default";
            }
            var policyConfig = config.Settings.dataProtection?[dataProtectionPolicy] as JObject;
            if (policyConfig == null)
            {
                throw new InvalidOperationException($"Cannot unprotect data : Policy config 'dataProtection.{dataProtectionPolicy}' not found.");
            }

            var providerId = policyConfig["provider"]?.ToObject<string>();

            if (providerId == null)
            {
                throw new InvalidOperationException($"Cannot unprotect data : 'dataProtection.{dataProtectionPolicy}.provider' not found.");
            }


            var provider = providers.FirstOrDefault(p => p.Id == providerId);
            if (provider == null)
            {
                throw new InvalidOperationException($"Cannot unprotect data: provider {providerId} not found.");
            }

            var cipherText = UrlBase64.Decode(value);
            return provider.Unprotect(cipherText, dataProtectionPolicy, policyConfig);
        }

        public async Task<string> ProtectBase64Url(byte[] value, string policy)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (policy is null)
            {
                throw new ArgumentNullException(nameof(policy));
            }


            var policyConfig = config.Settings.dataProtection?[policy] as JObject;
            if (policyConfig == null)
            {
                throw new InvalidOperationException($"Cannot protect data : Policy config 'dataProtection.{policy}' not found.");
            }

            var providerId = policyConfig["provider"]?.ToObject<string>();

            if (providerId == null)
            {
                throw new InvalidOperationException($"Cannot protect data : 'dataProtection.{policy}.provider' not found.");
            }


            var provider = providers.FirstOrDefault(p => p.Id == providerId);
            if (provider == null)
            {
                throw new InvalidOperationException($"Cannot protect data: provider {providerId} not found.");
            }

            var cipherText = await provider.Protect(value, policy, policyConfig);
            return policy + "|" + UrlBase64.Encode(cipherText);
        }




    }


    internal static class UrlBase64
    {
        private static readonly char[] TwoPads = { '=', '=' };

        public static string Encode(byte[] bytes)
        {
            var encoded = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');

            encoded = encoded.TrimEnd('=');


            return encoded;
        }

        public static byte[] Decode(string encoded)
        {
            var chars = new List<char>(encoded.ToCharArray());

            for (int i = 0; i < chars.Count; ++i)
            {
                if (chars[i] == '_')
                {
                    chars[i] = '/';
                }
                else if (chars[i] == '-')
                {
                    chars[i] = '+';
                }
            }

            switch (encoded.Length % 4)
            {
                case 2:
                    chars.AddRange(TwoPads);
                    break;
                case 3:
                    chars.Add('=');
                    break;
            }

            var array = chars.ToArray();

            return Convert.FromBase64CharArray(array, 0, array.Length);
        }
    }

}
