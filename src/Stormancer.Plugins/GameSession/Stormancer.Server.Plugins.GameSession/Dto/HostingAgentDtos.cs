using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    public class GetRunningContainersParameters
    {

    }
    public class GetRunningContainersResponse
    {
        [MessagePackMember(0)]
        public IEnumerable<ContainerDescription> Containers { get; set; }

        [MessagePackMember(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [MessagePackMember(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [MessagePackMember(3)]
        public int TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public int CurrentMemoryQuotaUsed { get; set; }
    }
    public class ContainerDescription
    {
        [MessagePackMember(0)]
        public string Id { get; set; }

        [MessagePackMember(1)]
        public string Image { get; set; }

        [MessagePackMember(2)]
        public DateTime CreatedOn { get; set; }

        [MessagePackMember(3)]
        public string AgentId { get; set; }

        [MessagePackMember(4)]
        public long CpuQuota { get; set; }

        [MessagePackMember(5)]
        public float CpuUsage { get; set; }

        [MessagePackMember(6)]
        public int MemoryQuota { get; set; }

        [MessagePackMember(7)]
        public int MemoryUsage { get; set; }
    }

    public class ContainerStartParameters
    {
        [MessagePackMember(0)]
        public string Image { get; set; }

        [MessagePackMember(1)]
        public string containerId { get; set; }

        [MessagePackMember(2)]
        public long cpuQuota { get; set; }

        [MessagePackMember(3)]
        public int MemoryQuota { get; set; }

        [MessagePackMember(4)]
        public Dictionary<string, string> EnvironmentVariables { get; internal set; }
    }

    public class ContainerStartResponse
    {
        [MessagePackMember(0)]
        public bool Success { get; set; }

        [MessagePackMember(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [MessagePackMember(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [MessagePackMember(3)]
        public int TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public int CurrentMemoryQuotaUsed { get; set; }
    }

    public class ContainerStopParameters
    {
        [MessagePackMember(0)]
        public string ContainerId { get; set; }

        [MessagePackMember(1)]
        public uint WaitBeforeKillSeconds { get; internal set; }
    }
    public class ContainerStopResponse
    {
        [MessagePackMember(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [MessagePackMember(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [MessagePackMember(3)]
        public int TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public int CurrentMemoryQuotaUsed { get; set; }
    }

    public class GetContainerLogsParameters
    {
        [MessagePackMember(0)]
        public string ContainerId { get; set; }

        [MessagePackMember(1)]
        public DateTime? Since { get; internal set; }

        [MessagePackMember(2)]
        public DateTime? Until { get; internal set; }

        [MessagePackMember(3)]
        public uint Size { get; internal set; }
    }

    public class GetContainerLogsResponse
    {
        [MessagePackMember(0)]
        public IEnumerable<string> StdOut { get; set; }

        [MessagePackMember(1)]
        public IEnumerable<string> StdErr { get; internal set; }
    }
}
