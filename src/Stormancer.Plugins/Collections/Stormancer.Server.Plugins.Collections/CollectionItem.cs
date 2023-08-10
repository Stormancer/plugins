using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{

    /// <summary>
    /// An item unlocked by an user.
    /// </summary>
    public class CollectionItemRecord
    {
        /// <summary>
        /// Gets or sets the unlocked item.
        /// </summary>
        [Required]
        [Key]
        public string ItemId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        [Required]
        [Key]
        public UserRecord User { get; set; } = default!;
    }
}
