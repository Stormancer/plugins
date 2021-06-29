using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server
{
    /// <summary>
    /// Attribute marking a method as a S2S API
    /// </summary>
    /// <remarks>
    /// Replaces Stormancer.Server.Plugins.API.ApiAttribute for S2S APIs.
    /// </remarks>
    public class S2SApiAttribute : Attribute
    {
        /// <summary>
        /// Route of the API.
        /// </summary>
        public string? Route { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating whether the proxy generator should generate a private implementation insteand of a public one.
        /// </summary>
        /// <remarks>
        /// Setting this option to true enables hiding the generated method and writing manually a public wrapper
        /// to perform additional processing to parameters or the return value.
        /// </remarks>
        public bool GeneratePrivateImpl { get; set; }
    }
}
