namespace Stormancer.GameServers.Agent
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DockerAgentConfigurationOptions _options;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;

            _options = new DockerAgentConfigurationOptions();
            configuration.Bind(_options.Section, _options);
            ClientFactory.SetConfigFactory(() =>
            {
                var config = Stormancer.ClientConfiguration.Create(_options.StormancerEndpoint, _options.StormancerAccount, _options.StormancerApplication);
                config.Plugins.Add(new GameServerAgentPlugin(config));
                return config;
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);


                

                var client = Stormancer.ClientFactory.GetClient(0);

                client.DependencyResolver

            }
        }
    }
}