using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    [Service(Named =false, ServiceType = "gameservers.agent")]
    public class AgentServerController :ControllerBase
    {
        private readonly AgentBasedGameServerProvider _gameServerProvider;

        public AgentServerController(AgentBasedGameServerProvider gameServerProvider) : base()
        {
            _gameServerProvider = gameServerProvider;
        }

        [S2SApi]
        public Task<IEnumerable<AgentDocument>> GetAgents()
        {
            var agents = _gameServerProvider.GetAgents();

            return Task.FromResult(agents.Select(a => new AgentDocument
            {
                Description = a.Description,
                Fault = a.Fault,
                Faulted = a.Faulted,
                Active = a.IsActive,
                ReservedCpu = a.ReservedCpu,
                ReservedMemory = a.ReservedMemory,
                TotalCpu = a.TotalCpu,
                TotalMemory = a.TotalMemory
            }));
        }

        [S2SApi]
        public Task<Dictionary<string,string>> GetRegions()
        {
            var result = new Dictionary<string, string>();
            var agents = _gameServerProvider.GetAgents();
            foreach(var agent in agents)
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
