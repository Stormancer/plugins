using MessagePack;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#if MSGPACK_CLI
using Key = MsgPack.Serialization.MessagePackMemberAttribute;
#endif

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    /// <summary>
    /// Crash report configuration of a container.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class CrashReportConfiguration
    {
       
        /// <summary>
        /// Gets or sets a bool indicating if crash reporting is enabled.
        /// </summary>
        [Key(0)]
        public bool Enabled { get; set; } = false;


        /// <summary>
        /// List of additional files in the container that should be bundled in the crash dump archive
        /// </summary>
        [Key(1)]
        public IEnumerable<string> AdditionalContainerFiles { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Includes
        /// </summary>
        [Key(2)]
        public bool IncludeOutput { get; set; } = true;

        /// <summary>
        /// Gets a boolean indicating if the crash report should include a crash dump.
        /// </summary>
        [Key(3)]
        public bool IncludeDump { get; set; } = true;
    }

    /// <summary>
    /// Parameters of a Get running containers request.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class GetRunningContainersParameters
    {

    }

    /// <summary>
    /// Response of a Get running container request.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class GetRunningContainersResponse
    {
        /// <summary>
        /// Gets the list of container on the agent.
        /// </summary>
        [Key(0)]
        public IEnumerable<ContainerDescription> Containers { get; set; } = Enumerable.Empty<ContainerDescription>();

        [Key(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [Key(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [Key(3)]
        public int TotalMemoryQuotaAvailable { get; set; }

        [Key(4)]
        public int CurrentMemoryQuotaUsed { get; set; }
    }

    /// <summary>
    /// Description of a container.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerDescription
    {
        [Key(0)]
        public string ContainerId { get; set; } = default!;


        [Key(1)]
        public string Image { get; set; } = default!;

        [Key(2)]
        public DateTime CreatedOn { get; set; }

        [Key(3)]
        public string AgentId { get; set; } = default!;

        [Key(4)]
        public float CpuQuota { get; set; }

        [Key(5)]
        public long MemoryQuota { get; set; }

        [Key(6)]
        public string Name { get; set; } = default!;

      
    }

    /// <summary>
    /// Parameters for a container start request.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerStartParameters
    {
        
        /// <summary>
        /// Gets or sets the image to use for the container.
        /// </summary>
        [Key(0)]
        public string Image { get; set; } = default!;


        /// <summary>
        /// Gets or sets the name of the container.
        /// </summary>
        [Key(1)]
        public string name { get; set; } = default!;


        /// <summary>
        /// Gets the CPU reserved for the container on the agent.
        /// </summary>
        [Key(2)]
        public float reservedCpu { get; set; }

        /// <summary>
        /// Gets the Memory reserved for the container on the agent.
        /// </summary>
        [Key(3)]
        public long reservedMemory { get; set; }

        /// <summary>
        /// Gets or sets environment variables that must be set for the container.
        /// </summary>
        [Key(4)]
        public Dictionary<string, string> EnvironmentVariables { get; internal set; } = default!;

        /// <summary>
        /// Deployment id of the app controlling the agent.
        /// </summary>
        [Key(5)]
        public string? AppDeploymentId { get; set; }

        /// <summary>
        /// Gets or sets an hard cpu limit the container cannot exceed.
        /// </summary>
        [Key(6)]
        public float cpuLimit { get; set; }

        /// <summary>
        /// Gets or sets an hard memory limit the container cannot exceed.
        /// </summary>
        [Key(7)]
        public long memoryLimit { get; set; }

        /// <summary>
        /// Gets or sets the configuration of the crash reporting system for this container.
        /// </summary>
        [Key(8)]
        public CrashReportConfiguration? CrashReportConfiguration { get; set; }

        /// <summary>
        /// Gets or sets the time in seconds the server must be kept alive.
        /// </summary>
        [Key(9)]
        public int KeepAliveSeconds { get;  set; }
    }

    /// <summary>
    /// Response of a container start request.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerStartResponse
    {
        /// <summary>
        /// Gets or sets a value indicating if the request was successful.
        /// </summary>
        [Key(0)]
        [MemberNotNullWhen(true,"Container")]
        [MemberNotNullWhen(false, "Error")]
        public bool Success { get; set; }

        [Key(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [Key(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [Key(3)]
        public long TotalMemoryQuotaAvailable { get; set; }

        [Key(4)]
        public long CurrentMemoryQuotaUsed { get; set; }

        [Key(5)]
        public ContainerDescription? Container { get; set; }

        [Key(6)]
        public string? Error { get; set; }
    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerStopParameters
    {
        [Key(0)]
        public string ContainerId { get; set; } = default!;

        [Key(1)]
        public uint WaitBeforeKillSeconds { get; internal set; }
    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerStopResponse
    {
        [Key(1)]
        public float TotalCpuQuotaAvailable { get; set; }

        [Key(2)]
        public float CurrentCpuQuotaUsed { get; set; }

        [Key(3)]
        public long TotalMemoryQuotaAvailable { get; set; }

        [Key(4)]
        public long CurrentMemoryQuotaUsed { get; set; }
    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class GetContainerLogsParameters
    {
        [Key(0)]
        public string ContainerId { get; set; } = default!;

        [Key(1)]
        public DateTime? Since { get;  set; }

        [Key(2)]
        public DateTime? Until { get;  set; }

        [Key(3)]
        public uint Size { get;  set; }

        [Key(4)]
        public bool Follow { get;  set; }
    }

    /// <summary>
    /// Argument to a KeepAlive RPC.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class KeepAliveContainerParameters
    {
        /// <summary>
        /// Gets or sets the id of the target container.
        /// </summary>
        [Key(0)]
        public string ContainerId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the time to keep the container alive, in seconds.
        /// </summary>
        [Key(1)]
        public int KeepAliveSeconds { get; set; } = 30 * 60;
    }

    /// <summary>
    /// Response of a KeepAlive RPC.
    /// </summary>
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class KeepAliveContainerResponse
    {
        /// <summary>
        /// Gets a boolean indicating if the operation was successful.
        /// </summary>
        [Key(0)]
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
#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class ContainerStatusUpdate
    {
        /// <summary>
        /// Gets or sets the CPU amount reserved on the agent.
        /// </summary>
        [Key(0)]
        public float ReservedCpu { get; set; }

        /// <summary>
        /// Gets or sets the total CPU amount available on the agent.
        /// </summary>
        [Key(1)]
        public float TotalCpu { get; set; }

        /// <summary>
        /// Gets or sets the memory reserved on the agent.
        /// </summary>
        [Key(2)]
        public long ReservedMemory { get; set; }

        /// <summary>
        /// Gets or sets the total memory available for containers on the agent.
        /// </summary>
        [Key(3)]
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets info about the container which state was updated.
        /// </summary>
        [Key(4)]
        public ContainerDescription Container { get; set; } = default!;

        /// <summary>
        /// Gets or sets the update type.
        /// </summary>
        [Key(5)]
        public ContainerStatusUpdateType EventType { get; set; }
    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class GetContainerStatsParameters
    {
        [Key(0)]
        public string ContainerId { get; set; } = default!;

        [Key(1)]
        public bool Stream { get;  set; }

        [Key(2)]
        public bool OneShot { get;  set; }
    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class GetContainerStatsResponse
    {

    }

#if !MSGPACK_CLI
    [MessagePackObject]
#endif
    public class AgentStatusDto
    {
        [Key(0)]
        public Dictionary<string, string> Claims { get; set; } = default!;

        [Key(1)]
        public string DockerVersion { get; set; } = default!;

        [Key(2)]
        public string AgentVersion { get; set; } = default!;

        [Key(3)]
        public string Error { get; set; } = default!;

        [Key(4)]
        public float ReservedCpu { get; set; }

        [Key(5)]
        public float TotalCpu { get; set; }

        [Key(6)]
        public long ReservedMemory { get; set; }

        [Key(7)]
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
            if(this.ApplicationUid != other.ApplicationUid) return false;

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

        
        /// <summary>
        /// Gets or sets the user id to use for this client.
        /// </summary>
        public string? ApplicationUid { get; set; }

        ///<inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as ApplicationConfigurationOptions);
        }
        ///<inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(StormancerEndpoint, StormancerAccount, StormancerApplication, UserId, ApplicationUid);
        }

        /// <inheritdoc/>
        public static bool operator ==(ApplicationConfigurationOptions? left, ApplicationConfigurationOptions? right)
        {
            return EqualityComparer<ApplicationConfigurationOptions>.Default.Equals(left, right);
        }

        ///<inheritdoc/>
        public static bool operator !=(ApplicationConfigurationOptions? left, ApplicationConfigurationOptions? right)
        {
            return !(left == right);
        }
    }
}
