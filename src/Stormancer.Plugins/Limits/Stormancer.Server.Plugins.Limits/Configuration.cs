using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Limits plugin configuration section.
    /// </summary>
    public class LimitsConfiguration
    {
        /// <summary>
        /// Configuration related to CCU limits.
        /// </summary>
        public ConnectionLimitsConfiguration connections { get; set; } = new ConnectionLimitsConfiguration();
    }

    /// <summary>
    /// CCU limit configuration.
    /// </summary>
    public class ConnectionLimitsConfiguration
    {
        /// <summary>
        /// Maximum number of concurrent user sessions authenticated on the application, 
        /// including slots reserved for reconnection. 
        /// </summary>
        /// <remarks>
        /// Defaults to -1 : unlimited.
        /// </remarks>
        public int max { get; set; } = -1;

        /// <summary>
        /// Maximum number of user sessions in the authentication queue.
        /// </summary>
        public int queue { get; set; } = 0;

        /// <summary>
        /// Reserved slots configuration.
        /// </summary>
        /// <remarks>
        /// Slots on the server are reserved when an user disconnects. An user with an active slot bypasses limits when reconnecting.
        /// Slots are lost after a configurable delay.
        /// </remarks>
        public SlotsConfiguration slots { get; set; } = new SlotsConfiguration();
    }

    /// <summary>
    /// Reserved Slots configuration.
    /// </summary>
    public class SlotsConfiguration
    {
        /// <summary>
        /// Duration in seconds the user slot can be recovered when manually disconnected.
        /// </summary>
        /// <remarks>
        /// Defaults to 0.
        /// </remarks>
        public int disconnected { get; set; } = 0;

        private int _connectionLost = -1;

        /// <summary>
        /// Duration in seconds the user slot can be recovered when connection is lost.
        /// </summary>
        /// <remarks>
        /// By defaults, or if set to a negative value, uses the value of <see cref="disconnected"/>.
        /// </remarks>
        public int connectionLost
        {
            get
            {
                return _connectionLost >= 0 ? _connectionLost : disconnected;
            }
            set
            {
                _connectionLost = value;
            }
        }
    }
}
