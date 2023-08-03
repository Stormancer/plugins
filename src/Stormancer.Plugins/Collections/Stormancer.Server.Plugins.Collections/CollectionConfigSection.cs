using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    /// <summary>
    /// Contains configuration properties for the collection system.
    /// </summary>
    public class CollectionConfigSection
    {
        /// <summary>
        /// Gets the path to the config section in the configuration.
        /// </summary>
        public const string SECTION_PATH = "collection";

        /// <summary>
        /// Gets or sets the path in the blob system fo the collectable item definition file.
        /// </summary>
        public string? ItemDefinitionsPath { get; set; }

        /// <summary>
        /// Gets or sets the duration the item definitions should be kept in the in memory cache before refreshing.
        /// </summary>
        /// <remarks>
        /// Defaults to 60s.
        /// </remarks>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(60);
    }
}
