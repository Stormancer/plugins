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
    }
}
