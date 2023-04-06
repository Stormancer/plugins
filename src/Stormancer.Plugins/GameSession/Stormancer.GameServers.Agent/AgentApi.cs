using Stormancer.Plugins;
using Stormancer.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Stormancer.Core;

namespace Stormancer.GameServers.Agent
{
    internal class AgentApi
    {
        private readonly Client _client;
        private readonly UserApi userApi;
        private readonly Stormancer.Diagnostics.ILogger _logger;

        public AgentApi(Client client, UserApi userApi, Stormancer.Diagnostics.ILogger logger)
        {
            _client = client;
            this.userApi = userApi;
            this._logger = logger;
        }

        public async Task StartAgent(int id, ApplicationConfigurationOptions applicationConfiguration, CancellationToken stoppingToken)
        {
            _client.Configuration.Application = applicationConfiguration.StormancerApplication;
            _client.Configuration.Account = applicationConfiguration.StormancerAccount;
            _client.Configuration.ServerEndpoints.Clear();
            _client.Configuration.ServerEndpoints.Add(applicationConfiguration.StormancerEndpoint);

            AgentGuid = id;
            ApplicationConfiguration = applicationConfiguration;
            await userApi.Login();
            var scene = await userApi.GetSceneForService("stormancer.plugins.serverPool");


            _logger.Log(Diagnostics.LogLevel.Info, "agent", "Docker daemon found.");

        }
        public IScene ServerPoolsScene { get; internal set; }
        public int AgentGuid { get; private set; }
        public ApplicationConfigurationOptions ApplicationConfiguration { get; private set; }
    }
}