using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
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
    /// A collectable item in the database.
    /// </summary>
    [PrimaryKey("ItemId", "UserId")]
    public class CollectionItemRecord
    {
        /// <summary>
        /// Gets or sets the unlocked item.
        /// </summary>
        [Required]
        public string ItemId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the id of the user owning the item.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        [Required]
        [ForeignKey("UserId")]
        public UserRecord User { get; set; } = default!;
    }
}
