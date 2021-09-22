using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    public class PartyGameFinder : IGameFinderAlgorithm
    {
        public JObject ComputeDataAnalytics(GameFinderContext gameFinderContext)
        {
            return new JObject();
        }

        public Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
        {
            var result = new GameFinderResult();
            foreach(var party in gameFinderContext.WaitingParties)
            {
                var game = new Game();
                var team = new Team(party);
                game.Teams.Add(team);
                result.Games.Add(game);
            }
            return Task.FromResult(result);
        }

        public Dictionary<string, int> GetMetrics()
        {
            return new Dictionary<string, int>();
        }

        public void RefreshConfig(string id, dynamic config)
        {
           
        }
    }
}
