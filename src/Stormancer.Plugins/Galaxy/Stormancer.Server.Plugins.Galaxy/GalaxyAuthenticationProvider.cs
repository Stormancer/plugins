using Jose;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Galaxy
{
    /// <summary>
    /// Represents the Galaxy configuration sections.
    /// </summary>
    public class GalaxyConfiguration
    {
        /// <summary>
        /// Id of the product.
        /// </summary>
        public string? productId { get; set; }

        /// <summary>
        /// Ticket encryption key
        /// </summary>
        /// <remarks>
        /// base64 encoded.
        /// </remarks>
        public string? ticketPrivateKey { get; set; }

        /// <summary>
        /// Server key.
        /// </summary>
        public string? serverKey { get; set; }
    }

    class GalaxyAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _config;
        private readonly ISecretsStore _secretsStore;
        private readonly ILogger _logger;
        private const int IV_SIZE = 16;

        public string Type => GalaxyConstants.PLATFORM_NAME;

        public GalaxyAuthenticationProvider(IUserService users, IConfiguration config, ILogger logger, ISecretsStore secretsStore)
        {
            _userService = users;
            _config = config;
            _logger = logger;
            _secretsStore = secretsStore;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
        }

        private async Task<string> DecryptSessionTicket(byte[] ticket)
        {
            // Cypher mode is AES 256 CBC
            // See : https://github.com/gogcom/galaxy-session-tickets-php/blob/master/src/GOG/SessionTickets/OpenSSLSessionTicketDecoder.php
            // https://docs.microsoft.com/fr-fr/dotnet/api/system.security.cryptography.aes?view=net-6.0

            if (ticket == null || ticket.Length <= 0)
            {
                throw new ArgumentNullException("ticket");
            }

            var c = _config.GetValue<GalaxyConfiguration>("galaxy");
            var ticketPrivateKeyPath = c.ticketPrivateKey;
            if (string.IsNullOrWhiteSpace(ticketPrivateKeyPath))
            {
                throw new InvalidOperationException("TicketPrivateKeyPath is not set in config.");
            }
            var ticketPrivateKey = await GetTicketPrivateKey(ticketPrivateKeyPath);

            var key = FromBase64Url(ticketPrivateKey);
            if (key == null || key.Length <= 0)
            {
                throw new InvalidOperationException("key is invalid.");
            }

            var IV = ticket.AsSpan(0, IV_SIZE).ToArray();

            if (IV == null || IV.Length <= 0)
            {
                throw new ArgumentNullException("IV is invalid.");
            }

            var cipherPayload = ticket.AsSpan(IV_SIZE).ToArray();

            if (cipherPayload == null || cipherPayload.Length <= 0)
            {
                throw new ArgumentNullException("cipher payload is invalid.");
            }

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = key;
            aesAlg.IV = IV;
            aesAlg.Padding = PaddingMode.Zeros;

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            // Create the streams used for decryption.
            using var msDecrypt = new MemoryStream(cipherPayload);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            // Read the decrypted bytes from the decrypting stream
            // and place them in a string.
            string decodedData = srDecrypt.ReadToEnd().TrimEnd('\0');

            if (string.IsNullOrWhiteSpace(decodedData))
            {
                throw new InvalidOperationException("Empty decoded data.");
            }

            return decodedData;
        }

        private async Task<string> GetTicketPrivateKey(string? ticketPrivateKeyPath)
        {
            if (string.IsNullOrWhiteSpace(ticketPrivateKeyPath))
            {
                throw new InvalidOperationException("Ticket private key store key is null.");
            }

            var secret = await _secretsStore.GetSecret(ticketPrivateKeyPath);
            if (secret == null || secret.Value == null)
            {
                throw new InvalidOperationException($"Missing secret '{ticketPrivateKeyPath}' in secrets store.");
            }

            return Encoding.UTF8.GetString(secret.Value) ?? "";
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var pId = new PlatformId { Platform = GalaxyConstants.PLATFORM_NAME };

            if (!authenticationCtx.Parameters.TryGetValue("ticket", out var ticketB64) || string.IsNullOrWhiteSpace(ticketB64))
            {
                return AuthenticationResult.CreateFailure("Galaxy session ticket must not be empty.", pId, authenticationCtx.Parameters);
            }

            try
            {
                var ticketData = FromBase64Url(ticketB64);
                var decodedData = await DecryptSessionTicket(ticketData);

                if (string.IsNullOrWhiteSpace(decodedData))
                {
                    throw new InvalidOperationException("Decoded data is null");
                }

                var obj = JsonSerializer.Deserialize<List<object>>(decodedData);
                if (obj == null)
                {
                    throw new InvalidOperationException("Decoded data format.");
                }

                var galaxyId = obj[0].ToString();
                if (string.IsNullOrWhiteSpace(galaxyId))
                {
                    throw new InvalidOperationException("Can't retrieve galaxyId.");
                }

                pId.PlatformUserId = galaxyId;

                var user = await _userService.GetUserByClaim(GalaxyConstants.PLATFORM_NAME, GalaxyConstants.GALAXYID_CLAIMPATH, galaxyId);

                if (user == null)
                {
                    var userId = Guid.NewGuid().ToString("N");

                    var galaxyUserData = new JObject();
                    galaxyUserData[GalaxyConstants.GALAXYID_CLAIMPATH] = galaxyId;

                    var userData = new JObject();
                    userData[GalaxyConstants.PLATFORM_NAME] = galaxyUserData;

                    user = await _userService.CreateUser(userId, userData, GalaxyConstants.PLATFORM_NAME);
                    user = await _userService.AddAuthentication(user, GalaxyConstants.PLATFORM_NAME, claim => claim[GalaxyConstants.GALAXYID_CLAIMPATH] = galaxyId, new Dictionary<string, string> { { GalaxyConstants.GALAXYID_CLAIMPATH, galaxyId } });
                }
                else
                {
                    if (user.LastPlatform != GalaxyConstants.PLATFORM_NAME)
                    {
                        user.LastPlatform = GalaxyConstants.PLATFORM_NAME;
                        await _userService.UpdateLastPlatform(user.Id, GalaxyConstants.PLATFORM_NAME);
                    }

                    bool updateUserData = false;

                    var galaxyUserData = user.UserData[GalaxyConstants.PLATFORM_NAME] ?? new JObject();

                    var userDataAccountId = galaxyUserData[GalaxyConstants.GALAXYID_CLAIMPATH];
                    if (userDataAccountId == null || userDataAccountId.ToString() != galaxyId)
                    {
                        galaxyUserData[GalaxyConstants.GALAXYID_CLAIMPATH] = galaxyId;
                        updateUserData = true;
                    }

                    if (updateUserData)
                    {
                        user.UserData[GalaxyConstants.PLATFORM_NAME] = galaxyUserData;
                        await _userService.UpdateUserData(user.Id, user.UserData);
                    }
                }

                return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, "authenticator.galaxy", $"Galaxy authentication failed. Ticket : {ticketB64}", ex);
                throw;
            }
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            return Task.FromResult(default(DateTime?));
        }

        public Task Unlink(User user)
        {
            return _userService.RemoveAuthentication(user, GalaxyConstants.PLATFORM_NAME);
        }

        internal static byte[] FromBase64Url(string toBeDecoded)
        {
            // Add padding
            toBeDecoded = toBeDecoded.PadRight(toBeDecoded.Length + (4 - toBeDecoded.Length % 4) % 4, '=');

            // Base64 URL encoded to Base 64 encoded
            toBeDecoded = toBeDecoded.Replace('-', '+').Replace('_', '/');

            // Base 64 decode
            return Convert.FromBase64String(toBeDecoded);
        }

        internal static string ToBase64Url(byte[] arg)
        {
            if (arg == null)
            {
                throw new ArgumentNullException("arg");
            }
            string s = Convert.ToBase64String(arg);
            s = s.Split(Base64PadCharacter)[0]; // Remove any trailing padding
            s = s.Replace(Base64Character62, Base64UrlCharacter62); // 62nd char of encoding
            s = s.Replace(Base64Character63, Base64UrlCharacter63); // 63rd char of encoding

            return s;
        }

        private const char Base64PadCharacter = '=';
        private static string DoubleBase64PadCharacter = string.Format(CultureInfo.InvariantCulture, "{0}{0}", Base64PadCharacter);
        private const char Base64Character62 = '+';
        private const char Base64Character63 = '/';
        private const char Base64UrlCharacter62 = '-';
        private const char Base64UrlCharacter63 = '_';
    }
}
