using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// 
    /// </summary>
    public class PartyGameSessionConfig : BaseGameSessionConfig
    {

    }

    /// <summary>
    /// Game finder creating a game session for each party entering matchmaking.
    /// </summary>
    public class PartyGameFinder : IGameFinderAlgorithm
    {
        private Action<IDependencyResolver, PartyGameSessionConfig, NewGame> onCreatingGame = (_, _, _) => { };

        /// <inheritdoc/>
        public JObject ComputeDataAnalytics(IGameFinderContext gameFinderContext)
        {
            return new JObject();
        }

        /// <inheritdoc/>
        public Task<GameFinderResult> FindGames(IGameFinderContext gameFinderContext)
        {
            var result = new GameFinderResult();
            foreach (var party in gameFinderContext.WaitingParties)
            {
                var game = new NewGame();
                game.Parties.Add(PartySummary.Create(party));

                var config = new PartyGameSessionConfig();
                onCreatingGame(gameFinderContext.Scope, config, game);
                var json = JObject.FromObject(config);
                game.PrivateCustomData.Merge(json);
                result.Games.Add(game);
            }
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Dictionary<string, int> GetMetrics()
        {
            return new Dictionary<string, int>();
        }

        /// <inheritdoc/>
        public void RefreshConfig(string id, dynamic config)
        {
            var options = PartyGameFinderExtensions.GetOptions<PartyGameFinderOptions>(id);

            onCreatingGame = options.onCreatingGame;
        }
    }
}
