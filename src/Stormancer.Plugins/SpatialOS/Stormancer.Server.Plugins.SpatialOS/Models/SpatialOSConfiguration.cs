using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.SpatialOS.Models
{
    /// <summary>
    /// SpatialOS configuration for the Stormancer application
    /// </summary>
    /// <remarks>
    /// Place this configuration in the configuration property "spatialos"
    /// </remarks>
    public class SpatialOSConfiguration
    {
        /// <summary>
        /// The SpatialOS service key. 
        /// </summary>
        public string ServiceKey { get; set; } = "";

        /// <summary>
        /// The SpatialOS project name
        /// </summary>
        public string ProjectName { get; set; } = "";

        /// <summary>
        /// The login token validity durations, in minutes
        /// </summary>
        public int LoginTokenDuration { get; set; }

        /// <summary>
        /// the default SpatialOS deployment name for generating login token. Can be overridden by an event handler.
        /// </summary>
        public string? DefaultDeploymentName { get; set; }
    }
}
