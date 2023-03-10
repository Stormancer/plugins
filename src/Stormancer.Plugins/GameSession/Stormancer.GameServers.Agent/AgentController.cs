using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System.Diagnostics;
using System.Linq;

namespace Stormancer.GameServers.Agent
{
    internal class AgentController
    {
        private readonly DockerService _docker;

        public AgentController(DockerService docker)
        {
            _docker = docker;
        }

        public UserApi? UserApi { get; set; }

        internal async Task<GetContainerLogsResponse> GetContainerLogs(GetContainerLogsParameters args, CancellationToken cancellationToken)
        {
            var result = await _docker.GetContainerLogsAsync(args.ContainerId,args.Since,args.Until, args.Size,cancellationToken);


            var response = new GetContainerLogsResponse { StdOut = result.StdOut.Split(Environment.NewLine), StdErr = result.StdErr.Split(Environment.NewLine) };
            return response;
        }

        internal async Task<GetRunningContainersResponse> GetRunningContainers(GetRunningContainersParameters args)
        {
            Debug.Assert(UserApi!=null);
            var containers = await _docker.ListContainers().ToListAsync();
            var response = new GetRunningContainersResponse { Containers = containers.Select(c=>new ContainerDescription { AgentId  = UserApi.UserId, Id = c.DockerContainerId, Image = c.Image, CreatedOn = c.Created,  }) };
        }

        internal async Task<ContainerStopResponse> StopContainer(ContainerStopParameters args)
        {
             await _docker.StopContainer(args.ContainerId, args.WaitBeforeKillSeconds);

            return new ContainerStopResponse {  };
        }

        internal async Task<ContainerStartResponse> TryStartContainer(ContainerStartParameters args)
        {
            await _docker.StartContainer(args.Image, args.containerId,new Dictionary<string, string>(),args.EnvironmentVariables,args.MemoryQuota,args.cpuQuota)
        }
    }
}