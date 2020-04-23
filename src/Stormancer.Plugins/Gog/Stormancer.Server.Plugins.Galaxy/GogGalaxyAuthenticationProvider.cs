﻿using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Galaxy
{
    /// <summary>
    /// Represents the Gog configuration sections.
    /// </summary>
    public class GogConfiguration
    {
        /// <summary>
        /// Id of the product.
        /// </summary>
        public string? ProductId { get; set; }

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
        public string? ServerKey { get; set; }
    }
    /// <summary>
    /// Gog Galaxy integration constants.
    /// </summary>
    public class GogConstants
    {
        /// <summary>
        /// The Gog galaxy auth provider type.
        /// </summary>
        public const string PROVIDER_NAME = "gog-galaxy";
    }
    class GogGalaxyAuthenticationProvider : IAuthenticationProvider
    {

        private const string CLAIM_PATH = "uid";
        private readonly IUserService users;
        private readonly IConfiguration config;
        private readonly ILogger logger;
        private const int IV_SIZE = 16;

        public string Type => GogConstants.PROVIDER_NAME;

        public GogGalaxyAuthenticationProvider(IUserService users, IConfiguration config, ILogger logger)
        {
            this.users = users;
            this.config = config;
            this.logger = logger;
        }

        public void AddMetadata(Dictionary<string, string> result)
        {

        }

        private string DecryptSessionTicket(byte[] ticket)
        {
            var c = config.GetValue<GogConfiguration>("gog");

            if (c.ticketPrivateKey == null)
            {
                throw new InvalidOperationException("Missing gog.ticketPrivateKey config value.");
            }
            var key = Convert.FromBase64String(c.ticketPrivateKey);

            //Cypher mode is AES 256 CBC
            // See : https://github.com/gogcom/galaxy-session-tickets-php/blob/master/src/GOG/SessionTickets/OpenSSLSessionTicketDecoder.php
            var iv = ticket.AsSpan(0, IV_SIZE);


            using (var aes = AesManaged.Create())
            using (var decryptor = aes.CreateDecryptor(key, iv.ToArray()))
            using (MemoryStream msDecrypt = new MemoryStream(ticket, IV_SIZE, ticket.Length - IV_SIZE))
            using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }

        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {


            var pId = new PlatformId { Platform = GogConstants.PROVIDER_NAME };
            if (!authenticationCtx.Parameters.TryGetValue("ticket", out var ticketB64) || string.IsNullOrWhiteSpace(ticketB64))
            {
                return AuthenticationResult.CreateFailure("Gog galaxy session ticket must not be empty.", pId, authenticationCtx.Parameters);
            }




            try
            {
                var ticketData = FromBase64Url(ticketB64);
                var plainText = DecryptSessionTicket(ticketData);


                var id = "xxx";
                pId.OnlineId = id;



               
                var user = await users.GetUserByClaim(GogConstants.PROVIDER_NAME, CLAIM_PATH, id);
             
                if (user == null)
                {
                    var uid = Guid.NewGuid().ToString("N");

                    user = await users.CreateUser(uid, JObject.FromObject(new { gogUserId = id }));

                    user = await users.AddAuthentication(user, GogConstants.PROVIDER_NAME, claim => claim[CLAIM_PATH] = id, new Dictionary<string, string> { { CLAIM_PATH, id } });
                }
               

                return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters);
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Debug, "authenticator.steam", $"Steam authentication failed. Ticket : {ticketB64}", ex);
                return AuthenticationResult.CreateFailure($"Invalid steam session ticket.", pId, authenticationCtx.Parameters);
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
            return users.RemoveAuthentication(user, GogConstants.PROVIDER_NAME);
        }



        internal static byte[] FromBase64Url(string toBeDecoded)
        {
            // Add padding
            toBeDecoded = toBeDecoded.PadRight(toBeDecoded.Length + (4 - toBeDecoded.Length % 4) % 4, '=');

            // Base64 URL encoded to Base 64 encoded
            toBeDecoded = toBeDecoded.Replace('-', '+').Replace('_', '/');

            // Base 64 decode
            byte[] raw = Convert.FromBase64String(toBeDecoded);
            return raw;
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
        private static string DoubleBase64PadCharacter = String.Format(CultureInfo.InvariantCulture, "{0}{0}", Base64PadCharacter);
        private const char Base64Character62 = '+';
        private const char Base64Character63 = '/';
        private const char Base64UrlCharacter62 = '-';
        private const char Base64UrlCharacter63 = '_';
    }
}
