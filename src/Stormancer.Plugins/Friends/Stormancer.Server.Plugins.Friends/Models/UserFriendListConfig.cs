using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends.Models
{
    /// <summary>
    /// Configuration of a friend list user.
    /// </summary>
    public class UserFriendListConfig
    {

        /// <summary>
        /// Status as set by the user
        /// </summary>
        public required FriendListStatusConfig Status { get; set; }

        /// <summary>
        /// Additional persisted custom data (status text, etc...)
        /// </summary>
        public required JObject CustomData { get; set; }

    }
}
