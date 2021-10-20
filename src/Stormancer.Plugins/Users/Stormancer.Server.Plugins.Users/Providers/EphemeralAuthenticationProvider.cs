using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for DeviceIdentifier auth provider.
    /// </summary>
    public static class EphemeralAuthenticationConfigurationExtensions
    {
        /// <summary>
        /// Configures the ephemeral auth provider.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <remarks></remarks>
        /// <returns></returns>
        public static UsersConfigurationBuilder ConfigureEphemeral(this UsersConfigurationBuilder config, Func<EphemeralAuthConfigurationBuilder, EphemeralAuthConfigurationBuilder> builder)
        {
            var b = new EphemeralAuthConfigurationBuilder();

            b = builder(b);
            config.Settings[EphemeralAuthenticationProvider.PROVIDER_NAME] = JObject.FromObject(b);
            return config;
        }

    }
}

namespace Stormancer.Server.Plugins.Users
{

    /// <summary>
    /// Configures the ephemeral auth (anonymous) auth provider.
    /// </summary>
    public class EphemeralAuthConfigurationBuilder : AuthProviderConfigurationBuilderBase<EphemeralAuthConfigurationBuilder>
    {

    }
    /// <summary>
    /// Provides an ephemeral user 
    /// </summary>
    class EphemeralAuthenticationProvider : IAuthenticationProvider
    {
        public const string PROVIDER_NAME = "ephemeral";
        public const string IsEphemeralKey = "isEphemeral";
        
        public string Type => PROVIDER_NAME;

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
            pId.PlatformUserId = uid;
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
