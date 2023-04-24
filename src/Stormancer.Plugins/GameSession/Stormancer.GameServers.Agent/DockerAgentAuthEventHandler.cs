using Jose;
using Stormancer.Plugins;
using System.Security.Cryptography.X509Certificates;

namespace Stormancer.GameServers.Agent
{
    internal class DockerAgentAuthEventHandler : IAuthenticationEventHandler
    {
        private readonly DockerAgentConfigurationOptions _agentOptions;
        private readonly AgentApi _agentApi;

        public DockerAgentAuthEventHandler(DockerAgentConfigurationOptions agentOptions, AgentApi agentApi)
        {
            _agentOptions = agentOptions;
            _agentApi = agentApi;
        }

        public Task RetrieveCredentials(CredentialsContext ctx)
        {
            ArgumentNullException.ThrowIfNull(_agentApi.ApplicationConfiguration.PrivateKeyPath);
            var privateKey = new X509Certificate2(_agentApi.ApplicationConfiguration.PrivateKeyPath, _agentApi.ApplicationConfiguration.PrivateKeyPassword);

            var claims = new Dictionary<string, string>();
            foreach (var attr in _agentOptions.Attributes)
            {
                claims.Add($"attributes.{attr.Key}", attr.Value);
            }

            claims.Add("quotas.maxMemory", _agentOptions.MaxMemory.ToString());
            claims.Add("quotas.maxCpu", _agentOptions.MaxCpu.ToString());
            claims.Add("id", _agentOptions.Id);

            var jwt = Jose.JWT.Encode(claims, privateKey.GetRSAPrivateKey(), JwsAlgorithm.RS256, new Dictionary<string, object>
            {
                ["typ"] = "JWT",
                ["x5t"] = privateKey.Thumbprint
            });

            ctx.AuthParameters.Type = "stormancer.gameserver.agent";
            ctx.AuthParameters.Parameters["dockerAgent.jwt"] = jwt;

            return Task.CompletedTask;
        }
    }
}