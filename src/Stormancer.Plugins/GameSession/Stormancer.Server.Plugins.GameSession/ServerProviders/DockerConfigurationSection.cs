using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerProviders
{
    /// <summary>
    /// Requirements to start a container
    /// </summary>
    public class DockerContainerRequirements
    {
        /// <summary>
        /// The maximum CPU time ratio a game server in the pool can use.
        /// </summary>
        /// <remarks>
        /// Default value : 0.5
        /// </remarks>
        public float cpuLimit { get; set; } = 0.5f;

        /// <summary>
        /// The maximum physical memory a game server in the pool can use.
        /// </summary>
        /// <remarks>
        /// Default value : 300MB
        /// </remarks>
        public long memoryLimit { get; set; } = 300 * 1024 * 1024;

        /// <summary>
        /// The CPU time ratio reserved for a game server.
        /// </summary>
        /// <remarks>
        /// Default value : 0.5
        /// </remarks>
        public float reservedCpu { get; set; } = 0.5f;

        /// <summary>
        /// The physical memory reserved for a game server.
        /// </summary>
        /// <remarks>
        /// Reserved memory should be lower or equal to memoryLimit.
        /// Default value : 300MB
        /// </remarks>
        public long reservedMemory { get; set; } = 300 * 1024 * 1024;

    }

    /// <summary>
    /// Configuration of a docker image.
    /// </summary>
    public class DockerImageConfiguration
    {
        /// <summary>
        /// Resource requirements to start the image.
        /// </summary>
        public DockerContainerRequirements? Requirements { get; set; } = null;
    }

    /// <summary>
    /// Configuration section for docker based game server hosting.
    /// </summary>
    public class DockerConfigurationSection : IConfigurationSection<DockerConfigurationSection>
    {
        /// <inheritdoc/>
        public static string SectionPath { get; } = "docker";

        /// <inheritdoc/>
        public static DockerConfigurationSection Default { get; } = new DockerConfigurationSection();

        /// <summary>
        /// Credentials used to connect to docker accounts.
        /// </summary>
        public Dictionary<string, DockerCredentials> Auth { get; set; } = new Dictionary<string, DockerCredentials>();

        /// <summary>
        /// List of preloaded images with optional configuration.
        /// </summary>
        public Dictionary<string, DockerImageConfiguration> PreloadedImages { get; set; } = new Dictionary<string, DockerImageConfiguration>();

        /// <summary>
        /// Image templates
        /// </summary>
        public Dictionary<string, DockerImageConfiguration> Templates { get; set; } = new Dictionary<string, DockerImageConfiguration>();
        
    }
}
