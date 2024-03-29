﻿using Stormancer.Plugins;
using Stormancer.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Stormancer.Core;
using Stormancer.Server.Plugins.GameSession.ServerProviders;

namespace Stormancer.GameServers.Agent
{
    internal class AgentApi 
    {
        private readonly Client _client;
        private readonly UserApi _userApi;
        private readonly Stormancer.Diagnostics.ILogger _logger;

        private TaskCompletionSource _readyTcs = new TaskCompletionSource();
        public AgentApi(Client client, UserApi userApi, Stormancer.Diagnostics.ILogger logger)
        {
            _client = client;
            this._userApi = userApi;
            this._logger = logger;
        }

        public async Task StartAgent(int id, string uid, ApplicationConfigurationOptions applicationConfiguration, CancellationToken stoppingToken)
        {
            _client.Configuration.Application = applicationConfiguration.StormancerApplication;
            _client.Configuration.Account = applicationConfiguration.StormancerAccount;
            _client.Configuration.ServerEndpoints.Clear();
            _client.Configuration.ServerEndpoints.Add(applicationConfiguration.StormancerEndpoint);

            Id = id;
            AgentUid = uid;
            ApplicationConfiguration = applicationConfiguration;
            _userApi.OnGameConnectionStateChanged += OnConnectionStateChanged;


            await _userApi.Login();

            await _readyTcs.Task.WaitAsync(stoppingToken);

            _logger.Log(Diagnostics.LogLevel.Info, "agent", $"Client connected to {applicationConfiguration}.");

        }
        public IScene ServerPoolsScene { get; internal set; }

        public int Id { get;private set; }
        public string AgentUid { get; private set; }
        public ApplicationConfigurationOptions ApplicationConfiguration { get; private set; }

        public void OnConnectionStateChanged(GameConnectionStateCtx ctx)
        {
            if(ctx.State == GameConnectionState.Authenticated)
            {
                _ = ConnectScene();
            }
        }

        private async Task ConnectScene()
        {
            var scene = await _userApi.GetSceneForService("stormancer.plugins.serverPool");

        }


        internal void SetReady()
        {
            _readyTcs.TrySetResult();
        }
    }
}