using System;

namespace Stormancer.Server
{
    /// <summary>
    /// Marks a controller as declaring a service in the service locator.
    /// </summary>
    public class ServiceAttribute : Attribute
    {

        /// <summary>
        /// Gets the type of the service.
        /// </summary>
        public string? ServiceType { get; set; }

        /// <summary>
        /// Is the service named ?
        /// </summary>
        /// <remarks>
        /// A named service controller must implement Stormancer.Server.Plugins.API.IServiceMetadata
        /// </remarks>
        public bool Named { get; set; }
    }


}
