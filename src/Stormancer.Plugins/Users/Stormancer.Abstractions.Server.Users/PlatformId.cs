using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Represents the Id of an user in a platform.
    /// </summary>
    /// <remarks>
    /// An user may be authenticated to several platforms, in this case he will be associated to a main <see cref="PlatformId"/> and secondary ones.
    /// </remarks>
    public struct PlatformId
    {

        /// <summary>
        /// Gets a string rep
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Platform + ":" + PlatformUserId;
        }

        /// <summary>
        /// Parses a platform id string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PlatformId Parse(string value)
        {
            var els = value.Split(':');
            return new PlatformId { Platform = els[0], PlatformUserId = els[1] };
        }

        /// <summary>
        /// Platform used to authenticate the user.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Online id of the user in the platform.
        /// </summary>
        public string PlatformUserId { get; set; }

        /// <summary>
        /// Returns true if the platform id is "unknown".
        /// </summary>
        public bool IsUnknown
        {
            get
            {
                return Platform == "unknown";
            }
        }

        /// <summary>
        /// Represents an Unknow platform Id.
        /// </summary>
        public static PlatformId Unknown
        {
            get
            {
                return new PlatformId { Platform = "unknown", PlatformUserId = "" };
            }
        }

        /// <summary>
        /// Online id of the user in the platform.
        /// </summary>
        [Obsolete("This property is obsolete and has been renamed. Use PlatformUserId instead.", false)]
        public string OnlineId
        {
            get
            {
                return PlatformUserId;
            }
            set
            {
                PlatformUserId = value;
            }
        }
    }
}
