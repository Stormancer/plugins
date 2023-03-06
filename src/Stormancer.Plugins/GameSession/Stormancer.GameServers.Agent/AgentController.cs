using Stormancer.Server.Plugins.GameSession.ServerProviders;

namespace Stormancer.GameServers.Agent
{
    public class AgentController
    {
        private readonly DockerService _docker;

        public AgentController(DockerService docker)
        {
            _docker = docker;
        }

        internal Task<GetContainerLogsResponse> GetContainerLogs(GetContainerLogsParameters args)
        {
            throw new NotImplementedException();
        }

        internal Task<GetRunningContainersResponse> GetRunningContainers(GetRunningContainersParameters args)
        {
            throw new NotImplementedException();
        }

        internal Task<ContainerStopResponse> StopContainer(ContainerStopParameters args)
        {
            throw new NotImplementedException();
        }

        internal Task<ContainerStartResponse> TryStartContainer(ContainerStartParameters args)
        {
            throw new NotImplementedException();
        }
    }
}