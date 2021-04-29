
using MsgPack.Serialization;
using System;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// An analytics event sent by a game client/server.
    /// </summary>
    public class EventDto
    {
        /// <summary>
        /// Type of the event.
        /// </summary>
        [MessagePackMember(0)]
        public string Type { get; set; } = default!;
       
        /// <summary>
        /// Content of the event.
        /// </summary>
        [MessagePackMember(1)]
        public string Content { get; set; } = default!;

        /// <summary>
        /// Category of the analytic event.
        /// </summary>
        [MessagePackMember(2)]
        public string Category { get; set; } = default!;

        /// <summary>
        /// Date the event was created on.
        /// </summary>
        [MessagePackMember(3)]
        public DateTime CreatedOn { get; set; }
    }
}
