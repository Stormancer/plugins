using Docker.DotNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.DataProtection;
using Stormancer.Server.Plugins.GameSession.ServerPool;
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

    /// <summary>
    /// Configuration class for the Agent based gameserver hosting pools
    /// </summary>
    public class AgentPoolConfigurationSection : PoolConfiguration
    {
        public string Image { get; set; }
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        public uint TimeoutSeconds { get; set; }

        public override string type => "fromProvider";

        public string provider { get; set; } = GameServerAgentConstants.TYPE;

        /// <summary>
        /// The maximum CPU time ratio a game server in the pool can use.
        /// </summary>
        public float cpuLimit { get; set; } = 0.5f;

        /// <summary>
        /// The maximum physical memory a game server in the pool can use.
        /// </summary>
        public long memoryLimit { get; set; } = 300 * 1024 * 1024;

        /// <summary>
        /// The CPU time ratio reserved for a game server.
        /// </summary>
        public float reservedCpu { get; set; } = 0.5f;

        /// <summary>
        /// The physical memory reserved for a game server.
        /// </summary>
        /// <remarks>
        /// Reserved memory should be lower or equal to memoryLimit.
        /// </remarks>
        public int reservedMemory { get; set; } = 300 * 1024 * 1024;
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
            _section = _configuration.GetValue<GameServerAgentConfigurationSection>("gameservers.agents") ?? new GameServerAgentConfigurationSection();
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
            _certificates = LoadSigningCertificates();
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

        public Task Authenticating(LoggingInCtx loggingInCtx)
        {
            loggingInCtx.Context = "service";
            return Task.CompletedTask;
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

            id.Platform = GameServerAgentConstants.TYPE;
            id.PlatformUserId = claims["id"];

            var user = new User { Id = Guid.NewGuid().ToString() };

            user.UserData["claims"] = JObject.FromObject(claims);


            var result = AuthenticationResult.CreateSuccess(user, id, authenticationCtx.Parameters);

            //Declares the session as being of type "service" and not a game client. This is picked up by the gameversion plugin to disable game version checks.
            result.initialSessionData["stormancer.type"] = Encoding.UTF8.GetBytes("service");

            return result;

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

            TotalCpu = float.Parse(Description.Claims["quotas.maxCpu"]);
            TotalMemory = long.Parse(Description.Claims["quotas.maxMemory"]);
        }
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public string Id { get; }
        public IScenePeerClient Peer { get; }
        public Session Session { get; }
        public string? Fault { get; }
        public bool Faulted => Fault != null;

        public AgentDescription Description { get; }

        public float TotalCpu { get; set; }
        public float ReservedCpu { get; set; }
        public long TotalMemory { get; set; }
        public long ReservedMemory { get; set; }
        public bool IsActive { get; set; } = true;
    }
    public class AgentBasedGameServerProvider : IGameServerProvider
    {
        private object _syncRoot = new object();
        private Dictionary<string, DockerAgent> _agents = new();
        private readonly IEnvironment _environment;
        private readonly IDataProtector _dataProtector;
        private readonly Task<ApplicationInfos> _applicationInfos;
        public AgentBasedGameServerProvider(IEnvironment environment, IDataProtector dataProtector)
        {
            _environment = environment;
            _dataProtector = dataProtector;
            _applicationInfos = _environment.GetApplicationInfos();
            _environment.ActiveDeploymentChanged += OnActiveDeploymentChanged;
        }

        private void OnActiveDeploymentChanged(object? sender, ActiveDeploymentChangedEventArgs e)
        {
            ShuttingDown = true;
            if (!e.IsActive)
            {
                lock(_syncRoot)
                {
                    foreach(var (id,agent) in _agents)
                    {
                        agent.Peer.Send("agent.UpdateActiveApp",e.ActiveDeploymentId, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
                    }
                }
            }
        }

        public void AgentConnected(IScenePeerClient peer, Session agentSession)
        {
            lock (_syncRoot)
            {
                var agent = new DockerAgent(peer, agentSession);
                _agents.Add(agentSession.User.Id,agent);
                _ = SubscribeContainerStatusUpdate(agent,agent.CancellationTokenSource.Token);
            }
        }

        public void AgentDisconnected(IScenePeerClient _, Session agentSession)
        {
            lock (_syncRoot)
            {
                if (_agents.Remove(agentSession.User.Id, out var agent))
                {
                    agent.CancellationTokenSource.Cancel(false);
                }
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

        private async Task SubscribeContainerStatusUpdate(DockerAgent agent, CancellationToken cancellationToken)
        {
            _ = UpdateAgentStatus(agent, cancellationToken);
            await foreach (var update in GetContainerStatusUpdates(agent.Id, cancellationToken))
            {
                agent.TotalCpu = update.TotalCpu;
                agent.ReservedCpu = update.ReservedCpu;
                agent.TotalMemory = update.TotalMemory;
                agent.ReservedMemory = update.ReservedMemory;
            }
        }

        
        private async Task UpdateAgentStatus(DockerAgent agent,CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1000));
            while (!cancellationToken.IsCancellationRequested)
            {
                var status = await agent.Peer.RpcTask<bool, AgentStatusDto>("agent.getStatus", true, cancellationToken);
                agent.TotalCpu = status.TotalCpu;
                agent.ReservedCpu = status.ReservedCpu;
                agent.TotalMemory = status.TotalMemory;
                agent.ReservedMemory = status.ReservedMemory;

                await timer.WaitForNextTickAsync();
            }
        }

        private IAsyncEnumerable<ContainerStatusUpdate> GetContainerStatusUpdates(string agentId, CancellationToken cancellationToken)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            var observable = agent.Peer.Rpc<bool, ContainerStatusUpdate>("agent.getDockerEvents", true, cancellationToken);


            return observable.ToAsyncEnumerable();
        }

        public void DisableAgent(string agentId)
        {
            lock (_syncRoot)
            {
                if (_agents.TryGetValue(agentId, out var agent))
                {
                    agent.IsActive = false;
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

        public async Task<ContainerStartResponse> StartContainerAsync(string agentId, string image, string name, float reservedCpu, long reservedMemory,float cpuLimit, long memoryLimit, Dictionary<string, string> environmentVariables)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }
            var appInfos = await _applicationInfos;
            return await agent.Peer.RpcTask<ContainerStartParameters, ContainerStartResponse>("agent.tryStartContainer", new ContainerStartParameters
            {
                name = name,
                reservedCpu = reservedCpu,
                Image = image,
                reservedMemory = reservedMemory,
                EnvironmentVariables = environmentVariables,
                AppDeploymentId = appInfos.DeploymentId,
                cpuLimit = cpuLimit,
                memoryLimit = memoryLimit,

            });
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

            return agent.Peer.RpcTask<ContainerStopParameters, ContainerStopResponse>("agent.stopContainer", new ContainerStopParameters { ContainerId = containerId });
        }

        public IAsyncEnumerable<IEnumerable<string>> GetLogsAsync(string agentId, string containerId, bool follow, DateTime? since = null, DateTime? until = null, uint size = 0, CancellationToken cancellationToken = default)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            var observable = agent.Peer.Rpc<GetContainerLogsParameters, IEnumerable<string>>("agent.getLogs", new GetContainerLogsParameters
            {
                ContainerId = containerId,
                Follow = follow,
                Since = since,
                Until = until,
                Size = size

            }, cancellationToken);


            return observable.ToAsyncEnumerable();

        }


        public string Type => GameServerAgentConstants.TYPE;

        public bool ShuttingDown { get; private set; }

        public async Task<StartGameServerResult> TryStartServer(string id, string authenticationToken, JObject config, CancellationToken ct)
        {
            var agentConfig = config.ToObject<AgentPoolConfigurationSection>();
            var applicationInfo = await _environment.GetApplicationInfos();
            var fed = await _environment.GetFederation();
            var node = await _environment.GetNodeInfos();

            var udpTransports = node.Transports.First(t => t.Item1 == "raknet");



            var endpoints = string.Join(',', fed.current.endpoints);

            var environmentVariables = new Dictionary<string, string>
            {
               
                //    { "Stormancer_Server_Port", server.ServerPort.ToString() },
                    { "Stormancer_Server_ClusterEndpoints", endpoints },
                    //{ "Stormancer_Server_PublishedAddresses", server.PublicIp },
                    //{ "Stormancer_Server_PublishedPort", server.ServerPort.ToString() },
                    { "Stormancer_Server_AuthenticationToken", authenticationToken },
                    { "Stormancer_Server_Account", applicationInfo.AccountId },
                    { "Stormancer_Server_Application", applicationInfo.ApplicationName },
                    { "Stormancer_Server_TransportEndpoint", udpTransports.Item2.First().Replace(":","|") }
             };
            if (agentConfig != null && agentConfig.EnvironmentVariables != null)
            {
                foreach (var (key, value) in agentConfig.EnvironmentVariables)
                {
                    environmentVariables[key] = value;
                }
            }

            var tries = 0;
            while (tries < 4)
            {
                tries++;
                var agent = FindAgent(agentConfig.reservedCpu, agentConfig.reservedMemory);

                if (agent != null)
                {
                    var response = await StartContainerAsync(agent.Id, agentConfig.Image, id, agentConfig.reservedCpu, agentConfig.reservedMemory,agentConfig.cpuLimit,agentConfig.memoryLimit, environmentVariables);

                    agent.TotalCpu = response.TotalCpuQuotaAvailable;
                    agent.TotalMemory = response.TotalMemoryQuotaAvailable;
                    agent.ReservedCpu = response.CurrentCpuQuotaUsed;
                    agent.ReservedMemory = response.CurrentMemoryQuotaUsed;

                    if (response.Success)
                    {
                        return new StartGameServerResult(true, new GameServerInstance { Id = response.Container.ContainerId }, (agent.Id, response.Container.ContainerId));
                    }
                }
                await Task.Delay(500);
            }

            return new StartGameServerResult(false, null, null);

        }

        private DockerAgent? FindAgent(float cpuRequirement, long memoryRequirement)
        {
            lock (_syncRoot)
            {
                foreach (var (id, agent) in _agents)
                {
                    if (agent.IsActive && agent.TotalCpu - agent.ReservedCpu >= cpuRequirement && agent.TotalMemory - agent.ReservedMemory >= memoryRequirement)
                    {
                        return agent;
                    }
                }
            }
            return null;
        }

        public async Task StopServer(string id, object ctx)
        {
            (string agentId, string containerId) = (ValueTuple<string, string>)(ctx);

            var response = await StopContainer(agentId, containerId);

            

        }
    }


    public class AgentDescription
    {
        public string Id { get; set; }
        public Dictionary<string, string> Claims { get; set; }
    }




}
