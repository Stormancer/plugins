using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides an ephemeral user 
    /// </summary>
    class EphemeralAuthenticationProvider : IAuthenticationProvider
    {
        public const string ProviderName = "ephemeral";
        public const string IsEphemeralKey = "isEphemeral";
        
        public string Type => ProviderName;

        public EphemeralAuthenticationProvider()
        {

        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            result["provider.ephemeral"] = "enabled";
        }

        public Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var pId = new PlatformId { Platform = Type };
            var uid = Guid.NewGuid().ToString("N");
            pId.OnlineId = uid;
            var user = new User { Id = uid };
            user.Auth[Type] = new JObject();
            user.UserData[IsEphemeralKey] = true;

            return Task.FromResult(AuthenticationResult.CreateSuccess(user, pId, authenticationCtx.Parameters));
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
            throw new NotImplementedException();
        }

        public Task Unlink(User user)
        {
            throw new NotImplementedException();
        }
    }
}
