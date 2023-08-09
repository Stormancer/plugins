using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    /// <summary>
    /// The definition of a collectible.
    /// </summary>
    public class CollectableItemDefinition
    {
        /// <summary>
        /// Unique id of the item, used for unlocking.
        /// </summary>
        [MessagePackMember(0)]
        public int Id { get; set; }

        /// <summary>
        /// Name of the item.
        /// </summary>
        [MessagePackMember(1)]    
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category of the item.
        /// </summary>
        [MessagePackMember(2)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Get or sets tags associated with the item.
        /// </summary>
        [MessagePackMember(3)]
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets Metadata associated with the definition
        /// </summary>
        /// <remarks>
        /// Can be used to store associated game resource, any data used during the unlocking process (for instance resource costs, or prerequisites)
        /// </remarks>
        [MessagePackMember(4)]
        public JObject Metadata { get; set; } = new JObject();
    }

    public class CollectableItemDefinitionRecord
    {
        /// <summary>
        /// Unique id of the item, used for unlocking.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Name of the item.
        /// </summary>

     
        [MaxLength(64)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category of the item.
        /// </summary>

     
        [MaxLength(64)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Get or sets tags associated with the item.
        /// </summary>

        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets Metadata associated with the definition
        /// </summary>
        /// <remarks>
        /// Can be used to store associated game resource, any data used during the unlocking process (for instance resource costs, or prerequisites)
        /// </remarks>
        [Column(TypeName = "jsonb")]
        public string Metadata { get; set; } = default!;

    }
}
