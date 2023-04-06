using RakNet;
using Stormancer.Plugins;

namespace Stormancer.GameServers.Agent
{
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DockerService _dockerService;
        private readonly DockerAgentConfigurationOptions _options;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, AgentController controller, DockerService dockerService)
        {
            _logger = logger;
            _dockerService = dockerService;
            _options = new DockerAgentConfigurationOptions();
            configuration.Bind(_options.Section, _options);


            ClientFactory.SetConfigFactory(() =>
            {
                var config = Stormancer.ClientConfiguration.Create("", "","");
                config.Logger = new Logger(logger);
                config.Plugins.Add(new GameServerAgentPlugin(_options, controller, dockerService, this));
                config.Plugins.Add(new Stormancer.Plugins.AuthenticationPlugin());
                return config;
            });

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


            await _dockerService.StartAgent(stoppingToken);



            while (!stoppingToken.IsCancellationRequested)
            {
                foreach(var (id, app) in _options.Applications)
                {
                    bool found = false;
                    lock(_syncRoot)
                    {
                        found = _agents.Values.Any(c => c.AppConfiguration == app);
                    }
                    if(!found)
                    {
                        _ =RunAppAsync(app, stoppingToken);
                    }
                }
                await Task.Delay(1000, stoppingToken);
            }


        }

        private object _syncRoot = new object();
        private int _currentAgentId = int.MinValue;
        private async Task RunAppAsync(ApplicationConfigurationOptions applicationConfiguration, CancellationToken stoppingToken)
        {
            try
            {
                Stormancer.Client client;
                int i;
                lock (_syncRoot)
                {
                    i = _currentAgentId++;
                    client = ClientFactory.GetClient(i);
                    _agents[i] = new AgentContainer(i, client, applicationConfiguration);
                }
                await client.DependencyResolver.Resolve<AgentApi>().StartAgent(i, applicationConfiguration, stoppingToken);
            }
            catch(Exception ex) 
            {
                _logger.Log(LogLevel.Error, "failed to connect to application {app}. Error: {ex}", applicationConfiguration,ex);
            }
        }

        internal void AppDeploymentUpdated(int agentId, string activeDeploymentId)
        {
            lock (_syncRoot)
            {
                if (_agents.TryGetValue(agentId, out var agent))
                {
                    var app = agent.AppConfiguration;
                    //Reconnect
                    _= RunAppAsync(app, CancellationToken.None);
                }
            }
        }

        internal void DestroyAgent(int agentId)
        {
            lock (_syncRoot)
            {
                if (_agents.Remove(agentId, out var container))
                {
                    ClientFactory.ReleaseClient(container.Index);
                }

            }
        }

        private Dictionary<int, AgentContainer> _agents = new Dictionary<int, AgentContainer>();
    }

    internal record AgentContainer
    (
        int Index,
        Stormancer.Client Client,
        ApplicationConfigurationOptions AppConfiguration
    );
}