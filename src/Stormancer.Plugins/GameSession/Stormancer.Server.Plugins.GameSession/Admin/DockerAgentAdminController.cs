﻿using Microsoft.AspNetCore.Mvc;
using Nest;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.Admin
{
    [ApiController]
    [Route("_hostingAgents")]
    public class DockerAgentAdminController: ControllerBase
    {
        private readonly AgentBasedGameServerProvider _provider;

        public DockerAgentAdminController(AgentBasedGameServerProvider provider)
        {
            _provider = provider;
        }

        [HttpGet]
        [Route("agents")]
        [ProducesResponseType(200,Type=typeof(GetAgentsResponse))]
        public async Task<IActionResult> GetAgents()
        {
            var agents = _provider.GetAgents();

            return Ok(new GetAgentsResponse { Agents = agents.Select(a=> new AgentDocument { Description = a.Description, Fault = a.Fault, Faulted = a.Faulted }) });
        }
        [HttpGet]
        [Route("containers")]
        [ProducesResponseType(200, Type = typeof(GetContainersResponse))]
        public async Task<IActionResult> GetRunningContainers()
        {
            var response = await _provider.GetRunningContainers().ToListAsync();

            return Ok(new GetContainersResponse { Containers = response });
        }

        [HttpGet]
        [Route("containers/{agentId}/{containerId}/logs")]
        [ProducesResponseType(200,Type=typeof(GetContainerLogsResponse))]
        public async Task<IActionResult> GetContainerLogs(string agentId, string containerId)
        {
            var logs = await _provider.GetLogs(agentId, containerId);

            return Ok(logs);
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
    }
    public class GetContainersResponse
    {
        public IEnumerable<ContainerDescription> Containers { get; internal set; }
    }
}