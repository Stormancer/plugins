using RakNet;
using Stormancer.Plugins;

namespace Stormancer.GameServers.Agent
{
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DockerService _dockerService;
        private readonly ClientsManager _clientsManager;
        private readonly DockerAgentConfigurationOptions _options;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, AgentController controller, DockerService dockerService, ClientsManager clientsManager)
        {
            _logger = logger;
            _dockerService = dockerService;
            _clientsManager = clientsManager;
            _options = new DockerAgentConfigurationOptions();
            configuration.Bind(DockerAgentConfigurationOptions.Section, _options);


            ClientFactory.SetConfigFactory(() =>
            {
                var config = ClientConfiguration.Create("", "","");
                config.Logger = new Logger(logger);
                config.Plugins.Add(new GameServerAgentPlugin(_options, controller, dockerService, _clientsManager));
                config.Plugins.Add(new AuthenticationPlugin());
                return config;
            });

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


            await _dockerService.StartAgent(stoppingToken);
            _clientsManager.StoppingToken = stoppingToken;


            while (!stoppingToken.IsCancellationRequested)
            {

                foreach(var (id, app) in _options.Applications)
                {
                    _clientsManager.EnsureRunning(app);
                   
                }
                await Task.Delay(1000, stoppingToken);
            }


        }

       
       
       


       
    }

   
}