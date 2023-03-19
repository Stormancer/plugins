using Jose;
using Stormancer.Plugins;
using System.Security.Cryptography.X509Certificates;

namespace Stormancer.GameServers.Agent
{
    internal class DockerAgentAuthEventHandler : IAuthenticationEventHandler
    {
        private readonly DockerAgentConfigurationOptions _options;

        public DockerAgentAuthEventHandler(DockerAgentConfigurationOptions options)
        {
            _options = options;
        }

        public Task RetrieveCredentials(CredentialsContext ctx)
        {
            ArgumentNullException.ThrowIfNull(_options.PrivateKeyPath);
            var privateKey = new X509Certificate2(_options.PrivateKeyPath, _options.PrivateKeyPassword);

            var claims = new Dictionary<string, string>();
            foreach (var attr in _options.Attributes)
            {
                claims.Add($"attributes.{attr.Key}", attr.Value);
            }

            claims.Add("quotas.maxMemory", _options.MaxMemory.ToString());
            claims.Add("quotas.maxCpu", _options.MaxCpu.ToString());
            claims.Add("id", _options.Id);

            var jwt = Jose.JWT.Encode(claims, privateKey.GetRSAPrivateKey(), JwsAlgorithm.RS256, new Dictionary<string, object>
            {
                ["typ"] = "JWT",
                ["x5t"] = privateKey.Thumbprint
            });

            ctx.AuthParameters.Type = "stormanger.gameservers.agent";
            ctx.AuthParameters.Parameters["dockerAgent.jwt"] = jwt;

            return Task.CompletedTask;
        }
    }
}