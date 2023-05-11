using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{
    /// <summary>
    /// Regions configuration section
    /// </summary>
    public class RegionsConfigurationSection
    {
        /// <summary>
        /// Is the regions feature enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// Default region
        /// </summary>
        public string? DefaultRegion { get; set; }
    }
}
