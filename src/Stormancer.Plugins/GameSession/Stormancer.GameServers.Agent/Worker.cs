using RakNet;

namespace Stormancer.GameServers.Agent
{
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DockerAgentConfigurationOptions _options;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, AgentController controller)
        {
            _logger = logger;

            _options = new DockerAgentConfigurationOptions();
            configuration.Bind(_options.Section, _options);
            ClientFactory.SetConfigFactory(() =>
            {
                var config = Stormancer.ClientConfiguration.Create(_options.StormancerEndpoint, _options.StormancerAccount, _options.StormancerApplication);
                config.Plugins.Add(new GameServerAgentPlugin(_options, controller));
                return config;
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);




            var client = Stormancer.ClientFactory.GetClient(0);

            await client.DependencyResolver.Resolve<DockerService>().StartAgent(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(10000, stoppingToken);
            }


        }
    }
}