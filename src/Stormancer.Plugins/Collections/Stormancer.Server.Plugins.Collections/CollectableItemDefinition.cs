using MessagePack;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    /// <summary>
    /// Contains item definitions.
    /// </summary>
    [MessagePackObject]
    public class CollectableItemDefinitions
    {
        /// <summary>
        /// map of item definitions, keyed by id.
        /// </summary>
        public Dictionary<string, CollectableItemDefinition> Items { get; set; } = new Dictionary<string, CollectableItemDefinition>();
    }
    /// <summary>
    /// The definition of a collectible.
    /// </summary>
    [MessagePackObject]
    public class CollectableItemDefinition
    {


        /// <summary>
        /// Name of the item.
        /// </summary>
        [Key(0)]    
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Category of the item.
        /// </summary>
        [Key(1)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Get or sets tags associated with the item.
        /// </summary>
        [Key(2)]
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets Metadata associated with the definition
        /// </summary>
        /// <remarks>
        /// Can be used to store associated game resource, any data used during the unlocking process (for instance resource costs, or prerequisites)
        /// </remarks>
        [Key(3)]
        public JObject Metadata { get; set; } = new JObject();
    }

}
