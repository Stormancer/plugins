using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameSession.ServerProviders;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Subjects;

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

        internal IAsyncEnumerable<IEnumerable<string>> GetContainerLogs(int agentId, GetContainerLogsParameters args, CancellationToken cancellationToken)
        {
            return _docker.GetContainerLogsAsync(agentId,args.ContainerId, args.Since, args.Until, args.Size, args.Follow, cancellationToken);

        }

        internal async Task<GetRunningContainersResponse> GetRunningContainers(int agentId, GetRunningContainersParameters args)
        {
            Debug.Assert(UserApi != null);
            var containers = await _docker.ListContainers(agentId).ToListAsync();
            var response = new GetRunningContainersResponse
            {
                Containers = containers.Select(c => GetDesc(c))
            };
            return response;
        }

        internal async Task<ContainerStopResponse> StopContainer(int agentId, ContainerStopParameters args)
        {
            var result = await _docker.StopContainer(agentId, args.ContainerId, args.WaitBeforeKillSeconds);

            return new ContainerStopResponse
            {
                CurrentCpuQuotaUsed = _docker.UsedCpu,
                CurrentMemoryQuotaUsed = _docker.UsedMemory,
                TotalCpuQuotaAvailable = _docker.TotalCpu,
                TotalMemoryQuotaAvailable = _docker.TotalMemory
            };
        }


        [return: NotNullIfNotNull("container")]
        private ContainerDescription? GetDesc(ServerContainer? container)
        {
            if (container != null)
            {
                return new ContainerDescription
                {
                    AgentId = UserApi.UserId,
                    ContainerId = container.DockerContainerId,
                    Name = container.Name,
                    CpuQuota = container.CpuQuota,
                    CreatedOn = container.Created,
                    Image = container.Image,
                    MemoryQuota = container.Memory
                };
            }
            else
            {
                return null;
            }
        }
        internal async Task<ContainerStartResponse> TryStartContainer(int agentId, ContainerStartParameters args)
        {
            var result = await _docker.StartContainer(agentId,args.Image, args.name, UserApi.UserId, new Dictionary<string, string>(), args.EnvironmentVariables, args.MemoryQuota, args.cpuQuota);


            return new ContainerStartResponse
            {
                Success = result.Success,
                Container = GetDesc(result.Container),
                CurrentCpuQuotaUsed = _docker.UsedCpu,
                CurrentMemoryQuotaUsed = _docker.UsedMemory,
                TotalCpuQuotaAvailable = _docker.TotalCpu,
                TotalMemoryQuotaAvailable = _docker.TotalMemory
            };
        }

        internal async IAsyncEnumerable<GetContainerStatsResponse> GetContainerStats(int agentId, GetContainerStatsParameters args, CancellationToken cancellationToken)
        {
            await foreach (var item in _docker.GetContainerStatsAsync(agentId, args.ContainerId, args.Stream, args.OneShot, cancellationToken))
            {
                yield return new GetContainerStatsResponse();
            }
        }
        internal IAsyncEnumerable<ContainerStatusUpdate> SubscribeToContainerUpdates(int agentId, CancellationToken cancellationToken)
        {

            var subject = new Subject<ContainerStatusUpdate>();

            void OnContainerStateChanged(ServerContainerStateChange change)
            {
                if (change.Container.AgentId == agentId)
                {
                    subject.OnNext(new ContainerStatusUpdate
                    {
                        Container = GetDesc(change.Container),
                        EventType = (ContainerStatusUpdateType)change.Status,
                        ReservedCpu = _docker.UsedCpu,
                        ReservedMemory = _docker.UsedMemory,
                        TotalCpu = _docker.TotalCpu,
                        TotalMemory = _docker.TotalMemory
                    });
                }
            }
            _docker.OnContainerStateChanged += OnContainerStateChanged;
            IDisposable? disposable = null;
            disposable = cancellationToken.Register(() =>
            {
                _docker.OnContainerStateChanged -= OnContainerStateChanged;
                subject.OnCompleted();
                disposable?.Dispose();
            });
            if (cancellationToken.IsCancellationRequested)
            {
                subject.OnCompleted();
                disposable.Dispose();
            }

            return subject.ToAsyncEnumerable();
        }

        internal async Task<AgentStatusDto> GetAgentStatus()
        {
            var status = await _docker.GetStatus();

            return new AgentStatusDto
            {
                AgentVersion = status.AgentVersion,
                Claims = status.Claims,
                DockerVersion = status.DockerVersion,
                Error = status.Error
            };
        }
    }
}