using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder.Models
{
    /// <summary>
    /// This class contains information about the resolution of an open game session ticket.
    /// </summary>
    public class JoinOpenGameContext
    {
        /// <summary>
        /// The ticket to the open game session.
        /// </summary>
        public OpenGameSessionTicket GameSessionTicket { get; }

        internal JoinOpenGameContext(OpenGameSessionTicket gameSessionTicket)
        {
            GameSessionTicket = gameSessionTicket;
        }

        /// <summary>
        /// An optional resolution action to be executed for the players enlisted on the <see cref="GameSessionTicket"/>.
        /// </summary>
        /// <remarks>
        /// Players will always be sent a connection token to the game session first, regardless of the contents of this member.
        /// </remarks>
        public Func<IGameFinderResolutionWriterContext, Task>? ResolutionAction { get; set; }
    }
}
