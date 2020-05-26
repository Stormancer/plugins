using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Facebook
{

    internal class FacebookConfiguration
    {
        /// <summary>
        /// Facebook application id.
        /// </summary>
        public string? AppId { get; set; }

        /// <summary>
        /// Id of the secret in the secret store.
        /// </summary>
        public string SecretId { get; set; } = "facebook.secret";
    }
    internal class FacebookAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IConfiguration config;

        public string Type => "facebook";
        public FacebookAuthenticationProvider(IConfiguration config)
        {
            this.config = config;
        }
        public void AddMetadata(Dictionary<string, string> result)
        {
            var c = ((JObject?)(config.Settings.facebook))?.ToObject<FacebookConfiguration>();
            if (!string.IsNullOrEmpty(c?.AppId))
            {
                result["facebook.appId"] = c.AppId;
            }
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            if(!authenticationCtx.Parameters.TryGetValue("accessToken",out var accessToken))
            {
                return AuthenticationResult.CreateFailure("'accessToken' authentication parameter is required.", PlatformId.Unknown, authenticationCtx.Parameters);
            }

            throw new NotImplementedException();

        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            throw new NotImplementedException();
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            throw new NotImplementedException();
        }

        public Task Setup(Dictionary<string, string> parameters, Session? session)
        {
            throw new NotImplementedException();
        }

        public Task Unlink(User user)
        {
            throw new NotImplementedException();
        }
    }
}
