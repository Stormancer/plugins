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
        /// Name of the item.
        /// </summary>
        [MessagePackMember(0)]    
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Category of the item.
        /// </summary>
        [MessagePackMember(1)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Get or sets tags associated with the item.
        /// </summary>
        [MessagePackMember(2)]
        public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets Metadata associated with the definition
        /// </summary>
        /// <remarks>
        /// Can be used to store associated game resource, any data used during the unlocking process (for instance resource costs, or prerequisites)
        /// </remarks>
        [MessagePackMember(3)]
        public JObject Metadata { get; set; } = new JObject();
    }

}
