using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    public class CrashReportConfiguration
    {
        [MessagePackMember(0)]
        public bool Enabled { get; set; } = false;


        /// <summary>
        /// List of additional files in the container that should be bundled in the crash dump archive
        /// </summary>
        [MessagePackMember(1)]
        public IEnumerable<string> AdditionalContainerFiles { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Includes
        /// </summary>
        [MessagePackMember(2)]
        public bool IncludeOutput { get; set; } = true;

        [MessagePackMember(3)]
        public bool IncludeDump { get; set; } = true;
    }
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
        public string ContainerId { get; set; } = default!;


        [MessagePackMember(1)]
        public string Image { get; set; } = default!;

        [MessagePackMember(2)]
        public DateTime CreatedOn { get; set; }

        [MessagePackMember(3)]
        public string AgentId { get; set; } = default!;

        [MessagePackMember(4)]
        public float CpuQuota { get; set; }

        [MessagePackMember(5)]
        public long MemoryQuota { get; set; }

        [MessagePackMember(6)]
        public string Name { get; set; } = default!;

      
    }

    public class ContainerStartParameters
    {
        

        [MessagePackMember(0)]
        public string Image { get; set; } = default!;

        [MessagePackMember(1)]
        public string name { get; set; } = default!;

        [MessagePackMember(2)]
        public float reservedCpu { get; set; }

        [MessagePackMember(3)]
        public long reservedMemory { get; set; }

        [MessagePackMember(4)]
        public Dictionary<string, string> EnvironmentVariables { get; internal set; } = default!;

        /// <summary>
        /// Deployment id of the app controlling the agent.
        /// </summary>
        [MessagePackMember(5)]
        public string? AppDeploymentId { get; set; }

        [MessagePackMember(6)]
        public float cpuLimit { get; set; }

        [MessagePackMember(7)]
        public long memoryLimit { get; set; }


        [MessagePackMember(8)]
        public CrashReportConfiguration? CrashReportConfiguration { get; set; }

    }

    public class ContainerStartResponse
    {
        [MessagePackMember(0)]
        [MemberNotNullWhen(true,"Container")]
        [MemberNotNullWhen(false, "Error")]
        public bool Success { get; set; }

        [MessagePackMember(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [MessagePackMember(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [MessagePackMember(3)]
        public long TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public long CurrentMemoryQuotaUsed { get; set; }

        [MessagePackMember(5)]
        public ContainerDescription? Container { get; set; }

        [MessagePackMember(6)]
        public string? Error { get; set; }
    }

    public class ContainerStopParameters
    {
        [MessagePackMember(0)]
        public string ContainerId { get; set; } = default!;

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
        public long TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public long CurrentMemoryQuotaUsed { get; set; }
    }

    public class GetContainerLogsParameters
    {
        [MessagePackMember(0)]
        public string ContainerId { get; set; } = default!;

        [MessagePackMember(1)]
        public DateTime? Since { get;  set; }

        [MessagePackMember(2)]
        public DateTime? Until { get;  set; }

        [MessagePackMember(3)]
        public uint Size { get;  set; }

        [MessagePackMember(4)]
        public bool Follow { get;  set; }
    }

   

    public enum ContainerStatusUpdateType
    {
        Start,
        Stop,
        
    }
 
    public class ContainerStatusUpdate
    {
        [MessagePackMember(0)]
        public float ReservedCpu { get; set; }

        [MessagePackMember(1)]
        public float TotalCpu { get; set; }

        [MessagePackMember(2)]
        public long ReservedMemory { get; set; }

        [MessagePackMember(3)]
        public long TotalMemory { get; set; }

        [MessagePackMember(4)]
        public ContainerDescription Container { get; set; } = default!;

        [MessagePackMember(5)]
        public ContainerStatusUpdateType EventType { get; set; }
    }

    public class GetContainerStatsParameters
    {
        [MessagePackMember(0)]
        public string ContainerId { get; set; } = default!;

        [MessagePackMember(1)]
        public bool Stream { get;  set; }

        [MessagePackMember(2)]
        public bool OneShot { get;  set; }
    }

    public class GetContainerStatsResponse
    {

    }


    public class AgentStatusDto
    {
        [MessagePackMember(0)]
        public Dictionary<string, string> Claims { get; set; } = default!;

        [MessagePackMember(1)]
        public string DockerVersion { get; set; } = default!;

        [MessagePackMember(2)]
        public string AgentVersion { get; set; } = default!;

        [MessagePackMember(3)]
        public string Error { get; set; } = default!;

        [MessagePackMember(4)]
        public float ReservedCpu { get; set; }

        [MessagePackMember(5)]
        public float TotalCpu { get; set; }

        [MessagePackMember(6)]
        public long ReservedMemory { get; set; }

        [MessagePackMember(7)]
        public long TotalMemory { get; set; }


    }
}
