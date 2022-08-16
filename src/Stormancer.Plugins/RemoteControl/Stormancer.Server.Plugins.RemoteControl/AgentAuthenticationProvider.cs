using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    public class AgentAuthenticationConfigurationBuilder : AuthProviderConfigurationBuilderBase<AgentAuthenticationConfigurationBuilder>
    {

    }

    internal class AgentAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IConfiguration configuration;
        private readonly ISecretsStore secretsStore;

        public string Type => RemoteControlConstants.AUTHPROVIDER_TYPE;


        public AgentAuthenticationProvider(IConfiguration configuration, ISecretsStore secretsStore)
        {
            this.configuration = configuration;
            this.secretsStore = secretsStore;
        }
        public void AddMetadata(Dictionary<string, string> result)
        {
        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            var configSection = configuration.GetValue("remoteControl", new RemoteControlConfigSection());
            var pId = new PlatformId();
            var keyPath = configSection.AgentKeyPath;
            if (keyPath == null)
            {
                return AuthenticationResult.CreateFailure($"'remoteControl.{nameof(RemoteControlConfigSection.AgentKeyPath)}' configuration value not set.", pId, authenticationCtx.Parameters);
            }

            if (!authenticationCtx.Parameters.TryGetValue(RemoteControlConstants.AUTHENTICATION_KEYS_AGENTID, out var agentId))
            {
                return AuthenticationResult.CreateFailure($"'{RemoteControlConstants.AUTHENTICATION_KEYS_AGENTID}' not set.", pId, authenticationCtx.Parameters);
            }

            if (!authenticationCtx.Parameters.TryGetValue(RemoteControlConstants.AUTHENTICATION_KEYS_PASSWORD, out var password))
            {
                return AuthenticationResult.CreateFailure($"'{RemoteControlConstants.AUTHENTICATION_KEYS_PASSWORD}' not set.", pId, authenticationCtx.Parameters);
            }

            if (!authenticationCtx.Parameters.TryGetValue(RemoteControlConstants.AUTHENTICATION_KEYS_METADATA, out var metadata))
            {
                return AuthenticationResult.CreateFailure($"'{RemoteControlConstants.AUTHENTICATION_KEYS_METADATA}' not set.", pId, authenticationCtx.Parameters);
            }

            var passwordSecret = await secretsStore.GetSecret(keyPath);

            if (passwordSecret == null || passwordSecret.Value == null)
            {
                return AuthenticationResult.CreateFailure($"'{keyPath}' secret not found.", pId, authenticationCtx.Parameters);
            }

            if (password != Encoding.UTF8.GetString(passwordSecret.Value))
            {
                return AuthenticationResult.CreateFailure("'Password incorrect.", pId, authenticationCtx.Parameters);
            }

            pId.Platform = RemoteControlConstants.AUTHPROVIDER_TYPE;
            pId.PlatformUserId = agentId;
            var user = new User { Id = agentId };
            user.Auth[Type] = new JObject();
            user.UserData["agentMetadata"] = JObject.Parse(metadata); 

            return AuthenticationResult.CreateSuccess(user,pId, authenticationCtx.Parameters);
        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session) => Task.CompletedTask;

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext) => Task.FromResult<DateTime?>(null);


        public Task Unlink(User user) => Task.CompletedTask;
    }
}
