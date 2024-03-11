
using MessagePack;
using System;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// An analytics event sent by a game client/server.
    /// </summary>
    [MessagePackObject]
    public class EventDto
    {
        /// <summary>
        /// Type of the event.
        /// </summary>
        [Key(0)]
        public string Type { get; set; } = default!;
       
        /// <summary>
        /// Content of the event.
        /// </summary>
        [Key(1)]
        public string Content { get; set; } = default!;

        /// <summary>
        /// Category of the analytic event.
        /// </summary>
        [Key(2)]
        public string Category { get; set; } = default!;

        /// <summary>
        /// Date the event was created on. (linux timestamp: ms since epoch)
        /// </summary>
        [Key(3)]
        public long CreatedOn { get; set; }
    }
}
