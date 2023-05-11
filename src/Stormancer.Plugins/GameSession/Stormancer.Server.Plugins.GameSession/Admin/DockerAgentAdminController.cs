using Microsoft.AspNetCore.Mvc;
using Nest;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.Admin
{

    /// <summary>
    /// Admin controller used to manage server agents.
    /// </summary>
    [ApiController]
    [Route("_hostingAgents")]
    public class DockerAgentAdminController : ControllerBase
    {
        private readonly AgentBasedGameServerProvider _provider;
        private readonly AgentServerProxy _agentServerApi;

        /// <summary>
        /// Creates a new <see cref="DockerAgentAdminController"/> object.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="agentServerApi"></param>
        public DockerAgentAdminController(AgentBasedGameServerProvider provider, AgentServerProxy agentServerApi)
        {
            _provider = provider;
            _agentServerApi = agentServerApi;
        }


        /// <summary>
        /// Gets the game server agents connected to the app.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("agents")]
        [ProducesResponseType(200, Type = typeof(GetAgentsResponse))]
        public async Task<IActionResult> GetAgents(CancellationToken cancellationToken)
        {
            
            return Ok(new GetAgentsResponse
            {
                Agents = await _agentServerApi.GetAgents(cancellationToken)
            });
        }

        /// <summary>
        /// Gets the game servers currently ran by the agents.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("containers")]
        [ProducesResponseType(200, Type = typeof(GetContainersResponse))]
        public async Task<IActionResult> GetRunningGameServers()
        {
            var response = await _provider.GetRunningContainers().ToListAsync();

            return Ok(new GetContainersResponse { Containers = response });
        }

        /// <summary>
        /// Gets the logs of a game server.
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="containerId"></param>
        /// <param name="since"></param>
        /// <param name="until"></param>
        /// <param name="size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("containers/{agentId}/{containerId}/logs")]
        [ProducesResponseType(200, Type = typeof(GetContainerLogsResponse))]
        public async Task<IActionResult> GetContainerLogs(string agentId, string containerId, DateTime? since, DateTime? until, uint size, CancellationToken cancellationToken)
        {
            var logs = await _provider.GetLogsAsync(agentId, containerId, false, since, until, size, cancellationToken).ToListAsync();


            return Ok(new GetContainerLogsResponse { Logs = logs.SelectMany(b => b) });
        }

        /// <summary>
        /// Disables an agent, preventing it from being used to host new game server instances.
        /// </summary>
        /// <param name="agentId"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("agents/{agentId}")]
        public IActionResult DisableAgent(string agentId)
        {
            _provider.DisableAgent(agentId);
            return Ok();
        }
    }

    public class GetAgentsResponse
    {
        public IEnumerable<AgentDocument> Agents { get; set; }
    }
    public class AgentDocument
    {
        public bool Faulted { get; set; }
        public string? Fault { get; set; }

        public AgentDescription Description { get; set; }

        public bool Active { get; set; }

        public float ReservedCpu { get; set; }
        public long ReservedMemory { get; set; }
        public long TotalMemory { get; set; }
        public float TotalCpu { get; set; }
    }
    public class GetContainersResponse
    {
        public IEnumerable<ContainerDescription> Containers { get; internal set; }
    }

    public class GetContainerLogsResponse
    {
        public IEnumerable<string> Logs { get; set; }
    }
}
