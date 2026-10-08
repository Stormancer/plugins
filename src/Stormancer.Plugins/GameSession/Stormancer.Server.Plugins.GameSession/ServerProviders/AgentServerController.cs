using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    /// <summary>
    /// Provides services for Game server agents.
    /// </summary>
    [Service(Named =false, ServiceType = "gameservers.agent")]
    public class AgentServerController :ControllerBase
    {
        private readonly AgentBasedGameServerProvider _gameServerProvider;

        internal AgentServerController(AgentBasedGameServerProvider gameServerProvider) : base()
        {
            _gameServerProvider = gameServerProvider;
        }

        /// <summary>
        /// Gets a list of agents currently connected to the application.
        /// </summary>
        /// <param name="onlyActive"></param>
        /// <returns></returns>
        [S2SApi]
        public Task<IEnumerable<AgentDocument>> GetAgents(bool onlyActive)
        {
            var agents = _gameServerProvider.GetAgents();

            return Task.FromResult(agents.Where(a =>
            {
                if(onlyActive)
                {
                    return a.IsActive;
                }
                else
                {
                    return true;
                }
            }).Select(a => new AgentDocument
            {
                Description = a.Description,
                Faults = a.Faults,
                Faulted = a.Faulted,
                Active = a.IsActive,
                ReservedCpu = a.ReservedCpu,
                ReservedMemory = a.ReservedMemory,
                TotalCpu = a.TotalCpu,
                TotalMemory = a.TotalMemory,
                LastUpdated = a.LastStatusUpdate
                
            }));
        }

        /// <summary>
        /// Direct a specific agent to download a docker image.
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [S2SApi]
        public Task<VoidResponse> DownloadImage(string agentId, DownloadImageArguments args,CancellationToken cancellationToken)
        {
            return _gameServerProvider.DownloadImageAsync(agentId, args,cancellationToken);
        }


        /// <summary>
        /// Gets a list of regions and an endpoint to ping.
        /// </summary>
        /// <param name="onlyActive"></param>
        /// <returns></returns>
        [S2SApi]
        public Task<Dictionary<string,string>> GetRegions(bool onlyActive)
        {
            var result = new Dictionary<string, string>();
            var agents = _gameServerProvider.GetAgents().Where(a =>
            {
                if (onlyActive)
                {
                    return a.IsActive && !a.Faulted;
                }
                else
                {
                    return !a.Faulted;
                }
            });
            foreach (var agent in agents)
            {
                if(agent.Description.Region !=null && !result.TryGetValue(agent.Description.Region, out _) && agent.Description.WebApiEndpoint !=null && agent.IsActive)
                {
                    result[agent.Description.Region] = agent.Description.WebApiEndpoint; 
                }
            }
            return Task.FromResult(result);
        }

    }
}
