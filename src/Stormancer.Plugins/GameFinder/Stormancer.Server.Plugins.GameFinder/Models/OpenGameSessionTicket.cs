using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stormancer.Server.Plugins.GameFinder.Models
{
    /// <summary>
    /// A ticket for one or more players to join an open game session, created by an <see cref="IGameFinder"/> instance.
    /// </summary>
    public class OpenGameSessionTicket : IGameCandidate
    {
        /// <summary>
        /// Create a new ticket for an open game session.
        /// </summary>
        /// <param name="gameSession">The game session that this ticket is for.</param>
        /// <param name="teams">Teams to add to the open game session.</param>
        public OpenGameSessionTicket(OpenGameSession gameSession, params Team[] teams)
        {
            GameSession = gameSession;
            Teams = teams?.ToList() ?? new List<Team>();
        }

        /// <summary>
        /// Create a new ticket for an open game session.
        /// </summary>
        /// <param name="gameSession">The game session that this ticket is for.</param>
        /// <param name="teams">Teams to add to the open game session.</param>
        public OpenGameSessionTicket(OpenGameSession gameSession, IEnumerable<Team> teams) : this(gameSession, teams.ToArray())
        { }

        /// <summary>
        /// Create a new ticket for an open game session.
        /// </summary>
        /// <param name="gameSession">The game session that this ticket is for.</param>
        public OpenGameSessionTicket(OpenGameSession gameSession) : this(gameSession, Enumerable.Empty<Team>())
        { }

        /// <summary>
        /// The game session that this ticket is for.
        /// </summary>
        public OpenGameSession GameSession { get; }

        /// <summary>
        /// The teams that are going to join <see cref="GameSession"/>.
        /// </summary>
        public List<Team> Teams { get; }

        IEnumerable<Team> IGameCandidate.Teams { get => Teams; }

        public string Id { get => GameSession.SceneId; }
    }
}
