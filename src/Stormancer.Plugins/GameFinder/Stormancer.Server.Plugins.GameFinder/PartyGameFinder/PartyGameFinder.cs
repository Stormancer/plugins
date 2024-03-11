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
            var i = 0;
            foreach(var party in gameFinderContext.WaitingParties)
            {
                var game = new NewGame();
                var team = new Team(party) { TeamId = i.ToString() };
                i++;
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
