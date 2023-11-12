using Newtonsoft.Json.Linq;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Edgegap
{
    public class EdgegapPoolConfigurationSection : PoolConfiguration
    {
        public string? AppName { get; set; }
        public string? AppVersion { get; set; }

        public Dictionary<string, IEnumerable<Filter>> RegionsMapping { get; set; } = new Dictionary<string, IEnumerable<Filter>>();

        public override string type => "fromProvider";
    }

    internal class EdgegapServerProvider : IGameServerProvider
    {
        private readonly EdgegapClient _client;
        private readonly IEnvironment _environment;
        private readonly GameSessionEventsRepository _events;

        public string Type => "edgegap";


        public EdgegapServerProvider(EdgegapClient client, IEnvironment environment, GameSessionEventsRepository events)
        {
            _client = client;
            _environment = environment;
            _events = events;
        }
        public async Task StopServer(string id, object? context)
        {
            await _client.StopGameServerAsync((string)context, CancellationToken.None);
        }



        public async Task<GameSession.StartGameServerResult> TryStartServer(string id, string authToken, JObject config, IEnumerable<string> regions, CancellationToken cancellationToken)
        {
            var agentConfig = config.ToObject<EdgegapPoolConfigurationSection>();
            if (agentConfig == null || agentConfig.AppName == null)
            {
                return new GameSession.StartGameServerResult(false, null, null);
            }
            IEnumerable<Filter>? edgegapCountries = null;

            foreach (var region in regions)
            {
                if (agentConfig.RegionsMapping.TryGetValue(region, out edgegapCountries))
                {
                    break;
                }
            }


            var appInfos = await _environment.GetApplicationInfos();
            var fed = await _environment.GetFederation();
            var endpoints = string.Join(',', fed.current.endpoints);

            var args = new StartGameServerParameters
            {

                app_name = agentConfig.AppName,
                version_name = agentConfig.AppVersion,
                filters = edgegapCountries,

                env_vars = new List<EnvironmentVariable>{
                     new EnvironmentVariable{ key= "Stormancer_Server_ClusterEndpoints", value= endpoints },
                     new EnvironmentVariable{ key=  "Stormancer_Server_AuthenticationToken", value= authToken },
                     new EnvironmentVariable{ key=  "Stormancer_Server_Account",  value=appInfos.AccountId },
                     new EnvironmentVariable{ key=  "Stormancer_Server_Application", value= appInfos.ApplicationName },

                },
                tags = new List<string>
                {
                    "cluster:"+ fed.current.id,
                    $"app:{appInfos.AccountId}/{appInfos.ApplicationName}",
                    "id:"+id
                }
            };


            var r = await _client.StartGameServerAsync(args, cancellationToken);

            var evt = new GameSessionEvent { GameSessionId = id, Type = "gameye.startserver" };
            evt.CustomData["success"] = r.Success;
            evt.CustomData["gameye-location"] = JObject.FromObject(edgegapCountries ?? Enumerable.Empty<Filter>());
            evt.CustomData["edgegap-app"] = args.app_name;
            evt.CustomData["edgegap-version"] = args.version_name;


            if (r.Success)
            {
                evt.CustomData["edgegap-requestId"] = r.Value.request_id;
                evt.CustomData["edgegap-success"] = true;
                return new GameSession.StartGameServerResult(true, new GameServerInstance { Id = id }, r.Value.request_id);
            }
            else
            {
                evt.CustomData["edgegap-success"] = false;
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
