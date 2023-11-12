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
    /// <summary>
    /// Crash report configuration of a container.
    /// </summary>
    public class CrashReportConfiguration
    {
        /// <summary>
        /// Gets or sets a bool indicating if crash reporting is enabled.
        /// </summary>
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

        /// <summary>
        /// Gets a boolean indicating if the crash report should include a crash dump.
        /// </summary>
        [MessagePackMember(3)]
        public bool IncludeDump { get; set; } = true;
    }

    /// <summary>
    /// Parameters of a Get running containers request.
    /// </summary>
    public class GetRunningContainersParameters
    {

    }

    /// <summary>
    /// Response of a Get running container request.
    /// </summary>
    public class GetRunningContainersResponse
    {
        /// <summary>
        /// Gets the list of container on the agent.
        /// </summary>
        [MessagePackMember(0)]
        public IEnumerable<ContainerDescription> Containers { get; set; } = Enumerable.Empty<ContainerDescription>();

        [MessagePackMember(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [MessagePackMember(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [MessagePackMember(3)]
        public int TotalMemoryQuotaAvailable { get; set; }

        [MessagePackMember(4)]
        public int CurrentMemoryQuotaUsed { get; set; }
    }

    /// <summary>
    /// Description of a container.
    /// </summary>
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

    /// <summary>
    /// Parameters for a container start request.
    /// </summary>
    public class ContainerStartParameters
    {
        
        /// <summary>
        /// Gets or sets the image to use for the container.
        /// </summary>
        [MessagePackMember(0)]
        public string Image { get; set; } = default!;


        /// <summary>
        /// Gets or sets the name of the container.
        /// </summary>
        [MessagePackMember(1)]
        public string name { get; set; } = default!;


        /// <summary>
        /// Gets the CPU reserved for the container on the agent.
        /// </summary>
        [MessagePackMember(2)]
        public float reservedCpu { get; set; }

        /// <summary>
        /// Gets the Memory reserved for the container on the agent.
        /// </summary>
        [MessagePackMember(3)]
        public long reservedMemory { get; set; }

        /// <summary>
        /// Gets or sets environment variables that must be set for the container.
        /// </summary>
        [MessagePackMember(4)]
        public Dictionary<string, string> EnvironmentVariables { get; internal set; } = default!;

        /// <summary>
        /// Deployment id of the app controlling the agent.
        /// </summary>
        [MessagePackMember(5)]
        public string? AppDeploymentId { get; set; }

        /// <summary>
        /// Gets or sets an hard cpu limit the container cannot exceed.
        /// </summary>
        [MessagePackMember(6)]
        public float cpuLimit { get; set; }

        /// <summary>
        /// Gets or sets an hard memory limit the container cannot exceed.
        /// </summary>
        [MessagePackMember(7)]
        public long memoryLimit { get; set; }

        /// <summary>
        /// Gets or sets the configuration of the crash reporting system for this container.
        /// </summary>
        [MessagePackMember(8)]
        public CrashReportConfiguration? CrashReportConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the time in seconds the server must be kept alive.
        /// </summary>
        [MessagePackMember(9)]
        public int KeepAliveSeconds { get;  set; }
    }

    /// <summary>
    /// Response of a container start request.
    /// </summary>
    public class ContainerStartResponse
    {
        /// <summary>
        /// Gets or sets a value indicating if the request was successful.
        /// </summary>
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

    /// <summary>
    /// Argument to a KeepAlive RPC.
    /// </summary>
    public class KeepAliveContainerParameters
    {
        /// <summary>
        /// Gets or sets the id of the target container.
        /// </summary>
        [MessagePackMember(0)]
        public string ContainerId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the time to keep the container alive, in seconds.
        /// </summary>
        [MessagePackMember(1)]
        public int KeepAliveSeconds { get; set; } = 30 * 60;
    }

    /// <summary>
    /// Response of a KeepAlive RPC.
    /// </summary>
    public class KeepAliveContainerResponse
    {
        /// <summary>
        /// Gets a boolean indicating if the operation was successful.
        /// </summary>
        [MessagePackMember(0)]
        public bool Success { get; set; }
    }
   

    /// <summary>
    /// Container status update type.
    /// </summary>
    public enum ContainerStatusUpdateType
    {
        /// <summary>
        /// Container started.
        /// </summary>
        Start,

        /// <summary>
        /// Container stopped.
        /// </summary>
        Stop,
        
    }
 
    /// <summary>
    /// A container status update.
    /// </summary>
    public class ContainerStatusUpdate
    {
        /// <summary>
        /// Gets or sets the CPU amount reserved on the agent.
        /// </summary>
        [MessagePackMember(0)]
        public float ReservedCpu { get; set; }

        /// <summary>
        /// Gets or sets the total CPU amount available on the agent.
        /// </summary>
        [MessagePackMember(1)]
        public float TotalCpu { get; set; }

        /// <summary>
        /// Gets or sets the memory reserved on the agent.
        /// </summary>
        [MessagePackMember(2)]
        public long ReservedMemory { get; set; }

        /// <summary>
        /// Gets or sets the total memory available for containers on the agent.
        /// </summary>
        [MessagePackMember(3)]
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets info about the container which state was updated.
        /// </summary>
        [MessagePackMember(4)]
        public ContainerDescription Container { get; set; } = default!;

        /// <summary>
        /// Gets or sets the update type.
        /// </summary>
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


    /// <summary>
    /// An application the agent should connect to.
    /// </summary>
    public class ApplicationConfigurationOptions : IEquatable<ApplicationConfigurationOptions>
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{StormancerEndpoint}/{StormancerAccount}/{StormancerApplication}";
        }

        /// <inheritdoc/>
        public bool Equals(ApplicationConfigurationOptions? other)
        {
            if (other == null) return false;
            if (StormancerEndpoint != other.StormancerEndpoint) return false;
            if (StormancerAccount != other.StormancerAccount) return false;
            if (StormancerApplication != other.StormancerApplication) return false;

            return true;
        }

        /// <summary>
        /// Gets or sets the endpoint to connect to.
        /// </summary>
        public string? StormancerEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the account of the app to connect to.
        /// </summary>
        public string? StormancerAccount { get; set; }

        /// <summary>
        /// Gets or sets the name of the app to connect to.
        /// </summary>
        public string? StormancerApplication { get; set; }

        /// <summary>
        /// Gets or sets the userId the agent should use.
        /// </summary>
        public string? UserId { get; set; }

        public string? ApplicationUid { get; set; }

    }
}
