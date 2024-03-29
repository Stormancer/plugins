﻿using Newtonsoft.Json.Linq;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Gameye
{
    public class GameyePoolConfigurationSection : PoolConfiguration
    {
        public string? Image { get; set; }
        public string Region { get; set; } = "europe";

        public Dictionary<string, string> RegionsMapping { get; set; } = new Dictionary<string, string>();

        public override string type => "fromProvider";
    }

    internal class GameyeServerProvider : IGameServerProvider
    {
        private readonly GameyeClient _client;
        private readonly IEnvironment _environment;
        private readonly GameSessionEventsRepository _events;

        public string Type => "gameye";


        public GameyeServerProvider(GameyeClient client, IEnvironment environment, GameSessionEventsRepository events)
        {
            _client = client;
            _environment = environment;
            _events = events;
        }
        public async Task StopServer(string id, object? context)
        {
            await _client.StopGameServerAsync(id, CancellationToken.None);
        }



        public async Task<GameSession.StartGameServerResult> TryStartServer(string id, string authToken, JObject config, IEnumerable<string> regions, CancellationToken cancellationToken)
        {
            var agentConfig = config.ToObject<GameyePoolConfigurationSection>();
            if (agentConfig == null || agentConfig.Image == null)
            {
                return new GameSession.StartGameServerResult(false, null, null);
            }
            string? gameyeLocation = null;

            foreach (var region in regions)
            {
                if (agentConfig.RegionsMapping.TryGetValue(region, out gameyeLocation))
                {
                    break;
                }
            }

            if (gameyeLocation == null)
            {
                gameyeLocation = agentConfig.Region;
            }


            var appInfos = await _environment.GetApplicationInfos();
            var fed = await _environment.GetFederation();
            var endpoints = string.Join(',', fed.current.endpoints);

            var args = new StartGameServerParameters
            {
                Id = id,
                Image = agentConfig.Image,
                Location = gameyeLocation,
                Env = new Dictionary<string, string> {

                    { "Stormancer_Server_ClusterEndpoints", endpoints },
                    { "Stormancer_Server_AuthenticationToken", authToken },
                    { "Stormancer_Server_Account", appInfos.AccountId },
                    { "Stormancer_Server_Application", appInfos.ApplicationName },

                },
                Labels = new Dictionary<string, string>
                {
                    ["cluster"] = fed.current.id,
                    ["app"] = $"{appInfos.AccountId}/{appInfos.ApplicationName}"
                }
            };
            var r = await _client.StartGameServerAsync(args, cancellationToken);

            var evt = new GameSessionEvent{ GameSessionId = id, Type = "gameye.startserver"};
            evt.CustomData["success"] = r.Success;
            evt.CustomData["gameye-location"] = args.Location;
            evt.CustomData["gameye-image"] = args.Image;
          
            if (r.Success)
            {
                return new GameSession.StartGameServerResult(true, new GameServerInstance { Id = id }, null);
            }
            else
            {
                return new GameSession.StartGameServerResult(false, null, null);
            }
        }

   
        public IAsyncEnumerable<string> QueryLogsAsync(string id, DateTime? since, DateTime? until, uint size, bool follow, CancellationToken cancellationToken)
        {
            return _client.QueryLogsAsync(id, since, until, size, follow, cancellationToken);
        }

        public Task<bool> KeepServerAliveAsync(string gameSessionId, object? context)
        {
            return Task.FromResult(false);
        }
    }
}
