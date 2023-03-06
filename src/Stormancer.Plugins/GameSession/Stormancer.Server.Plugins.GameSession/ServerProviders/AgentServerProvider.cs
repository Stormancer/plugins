using Docker.DotNet.Models;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    /// <summary>
    /// Configuration section for gameserver agents.
    /// </summary>
    public class GameServerAgentConfigurationSection
    {
        /// <summary>
        /// List of paths in the cluster secret stores containing valid public keys for authenticating game server agents.
        /// </summary>
        public IEnumerable<string> AuthCertPaths { get; set; } = Enumerable.Empty<string>();
    }

    internal class GameServerAgentConfiguration : IConfigurationChangedEventHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ISecretsStore _secretsStore;
        private GameServerAgentConfigurationSection _section;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration"></param>
        public GameServerAgentConfiguration(IConfiguration configuration, ISecretsStore secretsStore)
        {
            _configuration = configuration;
            _secretsStore = secretsStore;
            _section = _configuration.GetValue<GameServerAgentConfigurationSection>("gameservers.agents");
            _certificates = LoadSigningCertificates();
        }

        private async Task<IEnumerable<X509Certificate2>> LoadSigningCertificates()
        {
            var certs = new List<X509Certificate2>();
            foreach (var path in _section.AuthCertPaths)
            {
                var secret = await _secretsStore.GetSecret(path);
                if (secret.Value != null)
                {
                    certs.Add(new X509Certificate2(secret.Value));
                }
            }
            return certs;
        }

        public void OnConfigurationChanged()
        {
            _section = _configuration.GetValue<GameServerAgentConfigurationSection>("gameservers.agents");

        }

        public GameServerAgentConfigurationSection ConfigurationSection => _section;

        private Task<IEnumerable<X509Certificate2>> _certificates;

        public async Task<X509Certificate2?> GetSigningCertificate(string thumbprint)
        {
            var certs = await _certificates;
            return certs.FirstOrDefault(cert => cert.Thumbprint == thumbprint);
        }
    }
    internal class GameServerAgentAuthenticationProvider : IAuthenticationProvider
    {
        private readonly GameServerAgentConfiguration _configuration;

        public string Type => GameServerAgentConstants.TYPE;


        public GameServerAgentAuthenticationProvider(GameServerAgentConfiguration configuration)
        {
            _configuration = configuration;
        }
        public void AddMetadata(Dictionary<string, string> result)
        {

        }

        public async Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct)
        {
            PlatformId id = new PlatformId();
            var jwt = authenticationCtx.Parameters["dockerAgent.jwt"];

            if (!Jose.JWT.Headers(jwt).TryGetValue("x5t", out var thumbprint))
            {
                return AuthenticationResult.CreateFailure("docker.Agent.jwt must contain an 'x5t' header.", id, new Dictionary<string, string>());
            }

            var certificate = await _configuration.GetSigningCertificate((string)thumbprint);

            if (certificate == null)
            {
                return AuthenticationResult.CreateFailure($"'{thumbprint}' is not an authorized certificate", id, new Dictionary<string, string>());
            }
            var claims = Jose.JWT.Decode<Dictionary<string, string>>(jwt, certificate.GetRSAPublicKey());

            id.Platform = "stormancer.gameservers.agents";
            id.PlatformUserId = claims["id"];

            var user = new User { Id = Guid.NewGuid().ToString() };

            user.UserData["claims"] = JObject.FromObject(claims);


            return AuthenticationResult.CreateSuccess(user, id, authenticationCtx.Parameters);

        }

        public Task OnGetStatus(Dictionary<string, string> status, Session session)
        {
            return Task.CompletedTask;
        }

        public Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext)
        {
            return Task.FromResult<DateTime?>(null);
        }

        public Task Unlink(User user)
        {
            throw new NotSupportedException();
        }
    }

    public static class GameServerAgentConstants
    {
        public const string TYPE = "stormancer.gameserver.agent";
    }

    public class DockerAgent
    {
        public DockerAgent(IScenePeerClient peer, Session session)
        {
            Id = session.User.Id;
            Peer = peer;
            Session = session;


            Description = new AgentDescription
            {
                Id = session.User.Id,
                Claims = session.User.UserData["claims"].ToObject<Dictionary<string, string>>()
            };
            if (Description.Claims.TryGetValue("fault", out var fault))
            {
                Fault = fault;
            }
        }

        public string Id { get; }
        public IScenePeerClient Peer { get; }
        public Session Session { get; }
        public string? Fault { get; }
        public bool Faulted => Fault != null;

        public AgentDescription Description { get; }
    }
    public class AgentBasedGameServerProvider : IGameServerProvider
    {
        private object _syncRoot = new object();
        private Dictionary<string, DockerAgent> _agents = new();

        public void AgentConnected(IScenePeerClient peer, Session agentSession)
        {
            lock (_syncRoot)
            {
                _agents.Add(agentSession.User.Id, new DockerAgent(peer, agentSession));
            }
        }

        public void AgentDisconnected(IScenePeerClient _, Session agentSession)
        {
            lock (_syncRoot)
            {
                _agents.Remove(agentSession.User.Id);
            }
        }

        public IEnumerable<DockerAgent> GetAgents()
        {
            lock (_syncRoot)
            {
                foreach (var (id, agent) in _agents)
                {
                    yield return agent;
                }
            }
        }

        public async IAsyncEnumerable<ContainerDescription> GetRunningContainers()
        {
            List<Task<IEnumerable<ContainerDescription>>> tasks = new List<Task<IEnumerable<ContainerDescription>>>();
            lock (_syncRoot)
            {
                foreach (var (id, agent) in _agents)
                {
                    tasks.Add(GetRunningContainers(agent.Peer));
                }
            }

            foreach (var task in tasks)
            {
                IEnumerable<ContainerDescription>? result = null;
                try
                {
                    result = await task;

                }
                catch (Exception)
                {

                }
                if (result != null)
                {
                    foreach (var container in await task)
                    {
                        yield return container;
                    }
                }
            }
        }

        public Task<IEnumerable<ContainerDescription>> GetRunningContainers(IScenePeerClient peer)
        {
            return peer.RpcTask<bool, IEnumerable<ContainerDescription>>("agent.getRunningContainers", true);
        }

        public Task<ContainerStartResponse> StartContainer(string agentId, string image, string containerId, float cpuQuota, int memoryQuota)
        {
            DockerAgent? agent;
            lock(_syncRoot)
            {
                if(!_agents.TryGetValue(agentId,out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            return agent.Peer.RpcTask<ContainerStartParameters,ContainerStartResponse>("agent.tryStartContainer", new ContainerStartParameters { containerId = containerId, cpuQuota = cpuQuota, Image = image, MemoryQuota = memoryQuota  });
        }

        public Task<ContainerStopResponse> StopContainer(string agentId, string containerId)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            return agent.Peer.RpcTask<ContainerStopParameters, ContainerStopResponse>("agent.stopContainer", new ContainerStopParameters {ContainerId = containerId });
        }

        public Task<GetContainerLogsResponse> GetLogs(string agentId,string containerId )
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            return agent.Peer.RpcTask<GetContainerLogsParameters, GetContainerLogsResponse>("agent.getLogs", new GetContainerLogsParameters { ContainerId = containerId });
        }


        public string Type => GameServerAgentConstants.TYPE;

        public Task<GameServerInstance> StartServer(string id, JObject config, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task StopServer(string id)
        {
            throw new NotImplementedException();
        }
    }


    public class AgentDescription
    {
        public string Id { get; set; }
        public Dictionary<string, string> Claims { get; set; }
    }




}
