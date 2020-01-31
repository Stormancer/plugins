using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.GameFinder.Models
{
    /// <summary>
    /// A ticket for one or more players to join an open game session, created by an <see cref="IGameFinder"/> instance.
    /// </summary>
    public class OpenGameSessionTicket : IGameCandidate
    {
        /// <summary>
        /// The game session that this ticket is for.
        /// </summary>
        public OpenGameSession GameSession { get; }

        /// <summary>
        /// The teams that are going to join <see cref="GameSession"/>.
        /// </summary>
        public List<Team> Teams { get; } = new List<Team>();

        IEnumerable<Team> IGameCandidate.Teams { get => Teams; }

        public string Id { get => GameSession.SceneId; }
    }
}
