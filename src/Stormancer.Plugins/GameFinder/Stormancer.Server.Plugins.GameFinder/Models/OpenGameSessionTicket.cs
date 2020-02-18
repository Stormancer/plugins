// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Stormancer.Server.Plugins.Models;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameFinder
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

        /// <summary>
        /// Id of the GameSession scene for this ticket.
        /// </summary>
        public string Id { get => GameSession.SceneId; }
    }
}
