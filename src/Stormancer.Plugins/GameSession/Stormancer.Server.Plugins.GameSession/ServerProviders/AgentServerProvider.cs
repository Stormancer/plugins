using Docker.DotNet.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.DataProtection;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    /// <summary>
    /// Configuration section for game server agents.
    /// </summary>
    public class GameServerAgentsConfigurationSection
    {
        /// <summary>
        /// List of paths in the cluster secret stores containing valid public keys for authenticating game server agents.
        /// </summary>
        public IEnumerable<string> AuthCertPaths { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// List of urls to agents that should be connected to this app to host game servers.
        /// </summary>
        public IEnumerable<string> AgentUrls { get; set; } = Enumerable.Empty<string>();


        /// <summary>
        /// Gets or sets OAuth credentials used to authenticate the app with the agents.
        /// </summary>
        public OAuthClientCredentials? ClientCredentials { get; set; }
    }

    /// <summary>
    /// OAuth client credentials.
    /// </summary>
    public class OAuthClientCredentials : IEquatable<OAuthClientCredentials>
    {
        /// <summary>
        /// Gets or sets the clientId to use for tokens.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the path  to the client secret in the secret store.
        /// </summary>
        public string? ClientSecretPath { get; set; }

        /// <summary>
        /// Gets or sets the issuer used to get access tokens for agent management.
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets or sets the audience 
        /// </summary>
        public string? Audience { get; set; }

        /// <inheritdoc/>
        public bool Equals(OAuthClientCredentials? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return other.ClientId == ClientId && other.ClientSecretPath == ClientSecretPath && other.Audience == Audience && other.Issuer == Issuer;
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as OAuthClientCredentials);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(ClientId, ClientSecretPath, Issuer, Audience);
        }

        /// <inheritdoc/>
        public static bool operator ==(OAuthClientCredentials? left, OAuthClientCredentials? right)
        {
            return EqualityComparer<OAuthClientCredentials>.Default.Equals(left, right);
        }

        /// <inheritdoc/>
        public static bool operator !=(OAuthClientCredentials? left, OAuthClientCredentials? right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Configuration class for the Agent based game server hosting pools
    /// </summary>
    public class AgentPoolConfigurationSection : PoolConfiguration
    {
        /// <summary>
        /// Gets or sets the docker image to use when starting a server in this pool.
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Get or sets the maximum time a game server is authorized to run.
        /// </summary>
        /// <remarks>
        /// Set to 0 for unlimited duration.
        /// </remarks>
        public uint MaxDuration { get; set; }

        ///<inheritdoc/>
        public override string type => "fromProvider";

        ///<inheritdoc/>
        public string provider { get; set; } = GameServerAgentConstants.TYPE;

        /// <summary>
        /// The maximum CPU time ratio a game server in the pool can use.
        /// </summary>
        /// <remarks>
        /// Default value : 0.5
        /// </remarks>
        public float cpuLimit { get; set; } = 0.5f;

        /// <summary>
        /// The maximum physical memory a game server in the pool can use.
        /// </summary>
        /// <remarks>
        /// Default value : 300MB
        /// </remarks>
        public long memoryLimit { get; set; } = 300 * 1024 * 1024;

        /// <summary>
        /// The CPU time ratio reserved for a game server.
        /// </summary>
        /// <remarks>
        /// Default value : 0.5
        /// </remarks>
        public float reservedCpu { get; set; } = 0.5f;

        /// <summary>
        /// The physical memory reserved for a game server.
        /// </summary>
        /// <remarks>
        /// Reserved memory should be lower or equal to memoryLimit.
        /// Default value : 300MB
        /// </remarks>
        public int reservedMemory { get; set; } = 300 * 1024 * 1024;


        /// <summary>
        /// Configuration of the game server crash report system.
        /// </summary>
        public CrashReportConfiguration CrashReportConfiguration { get; set; } = new CrashReportConfiguration();

    }

    /// <summary>
    /// Response of a get access token request.
    /// </summary>
    public class ExchangeClientCredentialsResponse
    {
        /// <summary>
        /// Gets the access token.
        /// </summary>
        public string access_token { get; set; } = default!;

        /// <summary>
        /// Gets the type of the token ("Bearer")
        /// </summary>
        public string token_type { get; set; } = default!;
    }

    /// <summary>
    /// Configuration manager for docker agent integration.
    /// </summary>
    public class GameServerAgentConfiguration : IConfigurationChangedEventHandler
    {
        private readonly IConfiguration _configuration;
        private readonly ISecretsStore _secretsStore;
        private readonly IHttpClientFactory _httpClientFactory;
        private GameServerAgentsConfigurationSection _section;

        /// <summary>
        /// Creates a new <see cref="GameServerAgentConfiguration"/>
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="secretsStore"></param>
        public GameServerAgentConfiguration(IConfiguration configuration, ISecretsStore secretsStore, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _secretsStore = secretsStore;
            _httpClientFactory = httpClientFactory;
            _section = _configuration.GetValue<GameServerAgentsConfigurationSection>("gameservers.agents") ?? new GameServerAgentsConfigurationSection();
            _certificates = LoadSigningCertificates();
            _currentClientCredentials = _section.ClientCredentials;

        }

        private async Task<IEnumerable<X509Certificate2>> LoadSigningCertificates()
        {
            var certs = new List<X509Certificate2>();
            if (_section != null)
            {
                foreach (var path in _section.AuthCertPaths)
                {
                    var secret = await _secretsStore.GetSecret(path);
                    if (secret.Value != null)
                    {
                        certs.Add(new X509Certificate2(secret.Value));
                    }
                }
            }
            return certs;
        }


        void IConfigurationChangedEventHandler.OnConfigurationChanged()
        {
            _section = _configuration.GetValue<GameServerAgentsConfigurationSection>("gameservers.agents");
            _certificates = LoadSigningCertificates();

            if (_currentClientCredentials != _section.ClientCredentials)
            {
                _accessToken = null;
                _currentClientCredentials = _section.ClientCredentials;
            }
        }

        /// <summary>
        /// Gets the configuration object.
        /// </summary>
        public GameServerAgentsConfigurationSection ConfigurationSection => _section;

        private Task<IEnumerable<X509Certificate2>> _certificates;

        private DateTime _lastAccessTokenRequest = DateTime.MinValue;
        private Task<string?>? _accessToken;
        private OAuthClientCredentials? _currentClientCredentials;

        /// <summary>
        /// Gets the certificate of the agent.
        /// </summary>
        /// <param name="thumbprint"></param>
        /// <returns></returns>
        public async Task<X509Certificate2?> GetSigningCertificate(string thumbprint)
        {
            try
            {
                var certs = await _certificates;
                return certs.FirstOrDefault(cert => cert.Thumbprint == thumbprint);
            }
            catch (Exception)
            {
                _certificates = LoadSigningCertificates();
                throw;
            }
        }

        private async Task<string?> GetClientSecret()
        {
            var clientSecretPath = _currentClientCredentials?.ClientSecretPath;
            if (clientSecretPath != null)
            {
                var secret = await _secretsStore.GetSecret(clientSecretPath);
                if (secret.Value != null)
                {
                    return Encoding.UTF8.GetString(secret.Value);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }


        private async Task<string?> CreateAccessToken()
        {

            var secret = await GetClientSecret();
            var cred = _currentClientCredentials;
            if (secret != null && cred != null && cred.Issuer != null && cred.Audience != null && cred.ClientId != null)
            {
                return await CreateJwtFromClientCredentials(_httpClientFactory.CreateClient(), new Uri(cred.Issuer), cred.Audience, cred.ClientId, secret);
            }
            else
            {
                return null;
            }
        }

        private static async Task<string> CreateJwtFromClientCredentials(HttpClient client, Uri issuer, string audience, string clientId, string clientSecret)
        {
            var result = await client.PostAsJsonAsync(new Uri(issuer, "/oauth/token"), new
            {
                client_id = clientId,
                client_secret = clientSecret,
                audience = audience,
                grant_type = "client_credentials"
            });

            result.EnsureSuccessStatusCode();

            var jwtResponse = await result.Content.ReadFromJsonAsync<ExchangeClientCredentialsResponse>();

            Debug.Assert(jwtResponse != null);

            return jwtResponse.access_token;

        }


        private void EnsureAccessTokenUpdated()
        {
            if (_currentClientCredentials == null)
            {
                return;
            }
            if (_accessToken == null || _lastAccessTokenRequest < DateTime.UtcNow - TimeSpan.FromHours(23))
            {
                _lastAccessTokenRequest = DateTime.UtcNow;
                _accessToken = CreateAccessToken();
            }
        }
        internal Task<string?> GetAccessToken()
        {
            EnsureAccessTokenUpdated();
            if (_accessToken != null)
            {
                return _accessToken;
            }
            else
            {
                return Task.FromResult<string?>(null);
            }
        }
    }
    internal class GameServerAgentAuthenticationProvider : IAuthenticationProvider
    {
        private readonly GameServerAgentConfiguration _configuration;

        public string Type => GameServerAgentConstants.AGENT_AUTH_TYPE;


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

            id.Platform = GameServerAgentConstants.AGENT_AUTH_TYPE;
            id.PlatformUserId = claims["name"];

            var user = new User { Id = claims.TryGetValue("uid", out var uid) ? uid : Guid.NewGuid().ToString() };

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

    /// <summary>
    /// Constants
    /// </summary>
    public static class GameServerAgentConstants
    {
        /// <summary>
        /// Authentication type for gameserver agents.
        /// </summary>
        public const string AGENT_AUTH_TYPE = "stormancer.gameserver.agent";

        /// <summary>
        /// Type of the game server pool provider.
        /// </summary>

        public const string TYPE = "docker-agent";
    }

    /// <summary>
    /// A remote agent that can run game servers.
    /// </summary>
    public class DockerAgent
    {
        /// <summary>
        /// Creates a new <see cref="DockerAgent"/> object.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="session"></param>
        public DockerAgent(IScenePeerClient peer, Session session)
        {
            ArgumentNullException.ThrowIfNull(session.User);
            Id = session.User.Id;
            Peer = peer;
            Session = session;


            Description = new AgentDescription
            {
                Id = session.User.Id,
                Claims = session.User.UserData["claims"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>(),

            };
            if (Description.Claims.TryGetValue("fault", out var fault))
            {
                Faults.Add(fault);
            }

            TotalCpu = float.Parse(Description.Claims["quotas.maxCpu"]);
            TotalMemory = long.Parse(Description.Claims["quotas.maxMemory"]);

        }

        /// <summary>
        /// Cancellation token source used to signal the agent disconnected.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Unique ID of the agent.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Peer of the agent.
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// Session of the agent.
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Error that occurred on the agent.
        /// </summary>
        public List<string> Faults { get; set; } = new List<string>();

        /// <summary>
        /// Error state of the agent.
        /// </summary>
        public bool Faulted => Faults.Count != 0;

        public DateTime? FaultExpiration { get; set; }

        /// <summary>
        /// Description of the agent.
        /// </summary>
        public AgentDescription Description { get; }


        /// <summary>
        /// Total CPU available on the agent.
        /// </summary>
        public float TotalCpu { get; set; }

        /// <summary>
        /// Cpu currently reserved on the agent.
        /// </summary>
        public float ReservedCpu { get; set; }

        /// <summary>
        /// Total available memory on the agent.
        /// </summary>
        public long TotalMemory { get; set; }


        /// <summary>
        /// Memory currently reserved by game servers on the agent.
        /// </summary>
        public long ReservedMemory { get; set; }

        /// <summary>
        /// Gets or sets a boolean indicating whether the agent should be considered to start game servers.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Provides integration with docker server hosting agents.
    /// </summary>
    public class AgentBasedGameServerProvider : IGameServerProvider, IDisposable
    {
        private object _syncRoot = new object();
        private Dictionary<string, DockerAgent> _agents = new();
        private readonly IEnvironment _environment;
        private readonly IDataProtector _dataProtector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly GameSessionEventsRepository _events;
        private readonly GameServerAgentConfiguration _configuration;
        private readonly Task<ApplicationInfos> _applicationInfos;

        /// <summary>
        /// Creates an <see cref="AgentBasedGameServerProvider"/>
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="dataProtector"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="logger"></param>
        /// <param name="events"></param>
        /// <param name="configuration"></param>
        public AgentBasedGameServerProvider(
            IEnvironment environment,
            IDataProtector dataProtector,
            IHttpClientFactory httpClientFactory,
            ILogger logger,
            GameSessionEventsRepository events,
            GameServerAgentConfiguration configuration)
        {
            _environment = environment;
            _dataProtector = dataProtector;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _events = events;
            _configuration = configuration;
            _applicationInfos = _environment.GetApplicationInfos();
            _environment.ActiveDeploymentChanged += OnActiveDeploymentChanged;
            _disposedCts = new CancellationTokenSource();
            _disposedCancellationToken = _disposedCts.Token;
            _ = RunAsync();
        }
      
        private CancellationTokenSource _disposedCts;
        private CancellationToken _disposedCancellationToken;

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposedCts.Cancel();
        }
        private async Task RunAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            var fed = await _environment.GetFederation();
            while (!_disposedCancellationToken.IsCancellationRequested && !ShuttingDown)
            {
                try
                {
                    await timer.WaitForNextTickAsync(_disposedCancellationToken);

                    var token = await _configuration.GetAccessToken();

                    var client = _httpClientFactory.CreateClient();
                    if (token != null)
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                        

                        foreach (var url in _configuration.ConfigurationSection.AgentUrls)
                        {
                            if (!IsConnected(url))
                            {
                                try
                                {
                                    client.BaseAddress = new Uri(url);
                                    var appInfos = await _applicationInfos;
                                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposedCancellationToken);
                                    cts.CancelAfter(60000);
                                    var result = await client.PostAsJsonAsync("/clients/connect", new ApplicationConfigurationOptions
                                    {
                                        UserId = url,
                                        StormancerAccount = appInfos.AccountId,
                                        StormancerApplication = appInfos.ApplicationName,
                                        StormancerEndpoint = fed.current.endpoints.First(),
                                        ApplicationUid = appInfos.HostUrl
                                    }, cts.Token);
                                }
                                catch (Exception) { }
                            }
                        }
                    }

                }
                catch (Exception)
                {

                }
            }
        }

        private bool IsConnected(string url)
        {
            return _agents.ContainsKey(url);
        }

        private void OnActiveDeploymentChanged(object? sender, ActiveDeploymentChangedEventArgs e)
        {
            ShuttingDown = true;
            _disposedCts.Cancel();
            if (!e.IsActive)
            {
                lock (_syncRoot)
                {
                    foreach (var (id, agent) in _agents)
                    {
                        agent.Peer.Send("agent.UpdateActiveApp", e.ActiveDeploymentId, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
                    }
                }
            }
        }

        public void AgentConnected(IScenePeerClient peer, Session agentSession)
        {
            if(ShuttingDown)
            {
                peer.DisconnectFromServer("shuttingDown");
            }
            lock (_syncRoot)
            {
                var agent = new DockerAgent(peer, agentSession);
                _agents.Add(agentSession.User.Id, agent);
                _ = SubscribeContainerStatusUpdate(agent, agent.CancellationTokenSource.Token);
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


        private async Task UpdateAgentStatus(DockerAgent agent, CancellationToken cancellationToken)
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

        public async Task<ContainerStartResponse> StartContainerAsync(string agentId, string image, string name, float reservedCpu, long reservedMemory, float cpuLimit, long memoryLimit, Dictionary<string, string> environmentVariables, CrashReportConfiguration crashReportConfiguration, CancellationToken cancellationToken)
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
                CrashReportConfiguration = crashReportConfiguration,
                KeepAliveSeconds = GameSessionPlugin.SERVER_KEEPALIVE_SECONDS,

            }, cancellationToken);
        }


        private Task<ContainerStopResponse> StopContainer(string agentId, string containerId)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            return agent.Peer.RpcTask<ContainerStopParameters, ContainerStopResponse>("agent.stopContainer", new ContainerStopParameters
            {
                ContainerId = containerId
            });
        }

        private async Task<bool> KeepContainerAliveAsync(string agentId, string gameSessionId)
        {
            DockerAgent? agent;
            lock (_syncRoot)
            {
                if (!_agents.TryGetValue(agentId, out agent))
                {
                    throw new InvalidOperationException("Agent not found");
                }
            }

            var result = await agent.Peer.RpcTask<KeepAliveContainerParameters, KeepAliveContainerResponse>("agent.keepContainerAlive", new KeepAliveContainerParameters
            {
                ContainerId = gameSessionId,
                KeepAliveSeconds = GameSessionPlugin.SERVER_KEEPALIVE_SECONDS
            });

            return result.Success;
        }


        public string Type => GameServerAgentConstants.TYPE;

        public bool ShuttingDown { get; private set; }

        public async Task<StartGameServerResult> TryStartServer(string id, string authenticationToken, JObject config, IEnumerable<string> regions, CancellationToken ct)
        {

            var agentConfig = config.ToObject<AgentPoolConfigurationSection>() ?? new AgentPoolConfigurationSection();
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
            var tryResults = new List<ContainerStartResponse>();
            while (tries < 4)
            {
                tries++;
                var agent = FindAgent(agentConfig.reservedCpu, agentConfig.reservedMemory, regions);

                if (agent != null)
                {
                    using var cts = new CancellationTokenSource(30000);
                    ContainerStartResponse response;
                    try
                    {
                        response = await StartContainerAsync(agent.Id, agentConfig.Image, id, agentConfig.reservedCpu, agentConfig.reservedMemory, agentConfig.cpuLimit, agentConfig.memoryLimit, environmentVariables, agentConfig.CrashReportConfiguration, cts.Token);

                        agent.Faults.Clear();
                        agent.FaultExpiration = null;
                    }
                    catch (Exception ex)
                    {

                        if (agent.Faults.Count > 0)
                        {
                            await agent.Peer.DisconnectFromServer("faulted");
                        }
                        agent.Faults.Add(ex.ToString());
                        agent.FaultExpiration = DateTime.UtcNow.AddSeconds(30);
                        response = new ContainerStartResponse { Success = false, Error = ex.ToString() };
                    }
                    _logger.Log(LogLevel.Info, "docker.start", $"Sent start container command to agent {agent.Id} for gamesession '{id}'", new { agentConfig, agentId = agent.Id, gameSession = id, response }, id, agent.Id);
                    tryResults.Add(response);
                    agent.TotalCpu = response.TotalCpuQuotaAvailable;
                    agent.TotalMemory = response.TotalMemoryQuotaAvailable;
                    agent.ReservedCpu = response.CurrentCpuQuotaUsed;
                    agent.ReservedMemory = response.CurrentMemoryQuotaUsed;

                    if (response.Success)
                    {
                        var record = new GameSessionEvent { GameSessionId = id, Type = "dockerAgent" };
                        record.CustomData["agent"] = agent.Id;
                        record.CustomData["containerId"] = response.Container.ContainerId;
                        _events.PostEventAsync(record);

                        return new StartGameServerResult(true,
                            new GameServerInstance { Id = agent.Id + "/" + response.Container.ContainerId }, (agent.Id, response.Container.ContainerId))
                        {
                            Region = agent.Description.Region
                        };
                    }
                    else
                    {

                        if (response.Error != "unableToSatisfyResourceReservation")
                        {
                            _logger.Log(LogLevel.Warn, "docker", $"Failed to Start container : '{response.Error}'", new { tries, tryResults });

                        }
                    }
                }
                await Task.Delay(500);
            }

            return new StartGameServerResult(false, null, null);

        }

        private DockerAgent? FindAgent(float cpuRequirement, long memoryRequirement, IEnumerable<string> regions)
        {
            lock (_syncRoot)
            {
                foreach (var region in regions)
                {
                    foreach (var (id, agent) in _agents)
                    {

                        if ((!agent.Faulted ||
                            (agent.FaultExpiration != null &&
                            agent.FaultExpiration < DateTime.UtcNow)) &&
                            agent.IsActive && agent.TotalCpu - agent.ReservedCpu >= cpuRequirement &&
                            agent.TotalMemory - agent.ReservedMemory >= memoryRequirement
                            && agent.Description.Region == region)
                        {
                            return agent;
                        }
                    }
                }

                if (!regions.Any())
                {
                    foreach (var (id, agent) in _agents) //If no preferred region, take any of them.
                    {

                        if ((!agent.Faulted ||
                            (agent.FaultExpiration != null &&
                            agent.FaultExpiration < DateTime.UtcNow)) &&
                            agent.IsActive && agent.TotalCpu - agent.ReservedCpu >= cpuRequirement &&
                            agent.TotalMemory - agent.ReservedMemory >= memoryRequirement)
                        {
                            return agent;
                        }
                    }

                }
            }


            return null;
        }
        /// <inheritdoc/>
        public async Task StopServer(string id, object? ctx)
        {
            Debug.Assert(ctx != null);
            (string agentId, string containerId) = (ValueTuple<string, string>)(ctx);
            try
            {
                var response = await StopContainer(containerId, agentId);
            }
            catch (InvalidOperationException) //If agent not available, consider the container shut down.
            {

            }


        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<string> QueryLogsAsync(string id, DateTime? since, DateTime? until, uint size, bool follow, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var events = await _events.GetEventsAsync(id, cancellationToken);
            var evt = events.FirstOrDefault(evt => evt.CustomData.ContainsKey("agent"));

            var agentId = evt?.CustomData["agent"]?.ToObject<string>();

            if (agentId == null)
            {
                throw new ClientException("Agent id not found.");
            }

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
                ContainerId = id,
                Follow = follow,
                Since = since,
                Until = until,
                Size = size

            }, cancellationToken);


            await foreach (var block in observable.ToAsyncEnumerable())
            {
                foreach (var log in block)
                {
                    yield return log;
                }
            }
        }

        /// <inheritdoc/>
        public Task<bool> KeepServerAliveAsync(string gameSessionId, object? ctx)
        {
            Debug.Assert(ctx != null);
            (string agentId, string containerId) = (ValueTuple<string, string>)(ctx);

            return KeepContainerAliveAsync(agentId, gameSessionId);
        }
    }

    /// <summary>
    /// Description of an agent.
    /// </summary>
    public class AgentDescription
    {
        /// <summary>
        /// Id of the agent
        /// </summary>
        [MessagePackMember(0)]
        public string Id { get; set; } = default!;


        /// <summary>
        /// List of claims associated with the agent.
        /// </summary>
        [MessagePackMember(1)]
        public Dictionary<string, string> Claims { get; set; } = default!;


        /// <summary>
        /// Web Api endpoint of the agent.
        /// </summary>
        [MessagePackIgnore]
        public string? WebApiEndpoint => Claims.ContainsKey("agent.webApi") ? Claims["agent.webApi"] : null;

        /// <summary>
        /// Region the agent belongs to.
        /// </summary>
        [MessagePackIgnore]
        public string? Region => Claims.ContainsKey("agent.region") ? Claims["agent.region"] : null;
    }




}
