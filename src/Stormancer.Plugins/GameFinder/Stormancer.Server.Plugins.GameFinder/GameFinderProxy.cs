using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Provides access to gamefinder capabilities.
    /// </summary>
    partial class GameFinderProxy
    {
        /// <summary>
        /// Make this game session open to new players via the specified <paramref name="gameFinder"/>.
        /// </summary>
        /// <param name="data">Custom data that will be passed to <paramref name="gameFinder"/>.</param>
        /// <param name="gameSession"></param>
        /// <param name="gameFinder">Name of the GameFinder where the session will be made available.</param>
        /// <param name="ct">When this token is canceled, the game session will be withdrawn from <paramref name="gameFinder"/>.</param>
        /// <returns>
        /// An async enumerable that yields every Team that is added to the game session by <paramref name="gameFinder"/>.
        /// It completes when the game session has been withdrawn from <paramref name="gameFinder"/>.
        /// </returns>
        /// <remarks>
        /// When this method is called, the game session will be made available to the GameFinder designated by <paramref name="gameFinder"/>.
        /// From there, players in <paramref name="gameFinder"/> will be able to join it, provided <paramref name="gameFinder"/> implements the required logic.
        /// The game session stays open until <paramref name="ct"/> is canceled.
        /// </remarks>
        public async Task OpenGameSession(string gameFinder, JObject data, IGameSessionService gameSession, CancellationToken ct)
        {

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            void OnGameSessionCompleted()
            {
                cts?.Cancel();
            }

            try
            {
                gameSession.OnGameSessionCompleted += OnGameSessionCompleted;

                ct = cts.Token;

                ct.ThrowIfCancellationRequested();


                await foreach (var teams in this.OpenGameSession(gameFinder, data, ct))
                {
                    foreach (var team in teams)
                    {
                        gameSession.UpdateGameSessionConfig(c =>
                        {
                            var t = c.Teams.FirstOrDefault(t => t.TeamId == team.TeamId);
                            if (t != null)
                            {
                                t.Parties.AddRange(team.Parties);
                            }
                            else
                            {
                                c.Teams.Add(team);
                            }
                        });
                    }
                }

            }
            finally
            {
                gameSession.OnGameSessionCompleted -= OnGameSessionCompleted;
            }

        }

    }
}
