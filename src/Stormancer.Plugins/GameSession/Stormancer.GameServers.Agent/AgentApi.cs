using Stormancer.Plugins;
using Stormancer.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Stormancer.GameServers.Agent
{
    internal class AgentApi
    {
        private readonly DockerService dockerService;
        private readonly UserApi userApi;
        private readonly Stormancer.Diagnostics.ILogger _logger;

        public AgentApi(DockerService dockerService, UserApi userApi, Stormancer.Diagnostics.ILogger logger)
        {
            this.dockerService = dockerService;
            this.userApi = userApi;
            this._logger = logger;
        }

        public async Task StartAgent(CancellationToken stoppingToken)
        {
            await dockerService.StartAgent(stoppingToken);

            _logger.Log(Diagnostics.LogLevel.Info,"agent","Docker agent found.");
            await userApi.Login();
        }
    }
}