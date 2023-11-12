using Microsoft.AspNetCore.Mvc;
using Nest;
using Stormancer.Core;
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
      
        private readonly ISceneHost _scene;

        /// <summary>
        /// Creates a new <see cref="DockerAgentAdminController"/> object.
        /// </summary>
        /// <param name="scene"></param>
        public DockerAgentAdminController(ISceneHost scene)
        {
         
            _scene = scene;
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
            await using var scope = _scene.CreateRequestScope();

            return Ok(new GetAgentsResponse
            {
                Agents = await scope.Resolve<AgentServerProxy>().GetAgents(false,cancellationToken)
            });
        }



     
    }

    /// <summary>
    /// Response to a Get agent request.
    /// </summary>
    public class GetAgentsResponse
    {
        /// <summary>
        /// Gets the list of game server hosting agents connected to the application.
        /// </summary>
        public IEnumerable<AgentDocument> Agents { get; set; } = Enumerable.Empty<AgentDocument>();
    }

    /// <summary>
    /// A game server hosting agent.
    /// </summary>
    public class AgentDocument
    {
        /// <summary>
        /// Gets a boolean indicating whether the agent is in a faulted state.
        /// </summary>
        public bool Faulted { get; set; }

        /// <summary>
        /// Gets the agent's faults.
        /// </summary>
        public IEnumerable<string> Faults { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets informations about the agent.
        /// </summary>
        public AgentDescription Description { get; set; } = default!;

        /// <summary>
        /// Gets a boolean indicating if the agent is considered for new game servers.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets the number of CPU cores reserved on the agent.
        /// </summary>
        public float ReservedCpu { get; set; }

        /// <summary>
        /// Gets the amount of RAM reserved on the agent.
        /// </summary>
        public long ReservedMemory { get; set; }

        /// <summary>
        /// Gets the max amount of RAM that can be reserved on the agent.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets the max number of CPU cores that can be reserved on the agent.
        /// </summary>
        public float TotalCpu { get; set; }
    }

    /// <summary>
    /// Response to a Get containers query.
    /// </summary>
    public class GetContainersResponse
    {
        /// <summary>
        /// List of containers.
        /// </summary>
        public IEnumerable<ContainerDescription> Containers { get; internal set; } = Enumerable.Empty<ContainerDescription>();
    }

}
