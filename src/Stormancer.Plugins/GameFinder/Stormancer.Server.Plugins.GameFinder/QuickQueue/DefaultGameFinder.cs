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

using Nest;
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
    /// Quick matchmaking queue
    /// </summary>
    public class QuickQueueGameFinder : IGameFinderAlgorithm
    {
        private int teamSize;
        private int teamCount;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        public JObject ComputeDataAnalytics(GameFinderContext gameFinderContext)
        {
            return new JObject();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        public Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
        {
            var results = new GameFinderResult();
            if (!gameFinderContext.WaitingParties.Any())
            {
                return Task.FromResult(results);

            }
            var gameFound = false;

            var parties = gameFinderContext.WaitingParties.OrderByDescending(p => p.Players.Count).ToList();

            do
            {

                gameFound = false;

                var game = new Game();
                for (int teamId = 0; teamId < teamCount; teamId++)
                {
                    var team = new Models.Team();


                    for (int pivotId = 0; pivotId < parties.Count; pivotId++)
                    {
                        var pivot = parties[pivotId];

                        if (game.AllParties.Contains(pivot))
                        {
                            continue;
                        }

                        var list = new List<Party>();


                        list.Add(pivot);

                        for (int id = pivotId + 1; id < parties.Count; id++)
                        {
                            var candidate = parties[id];

                            if (game.AllParties.Contains(candidate))
                            {
                                continue;
                            }

                            if (list.Sum(p => p.Players.Count) + candidate.Players.Count <= teamSize)
                            {
                                list.Add(candidate);
                            }

                          

                        }

                        if (list.Sum(p => p.Players.Count) == teamSize)
                        {
                            foreach (var p in list)
                            {
                                team.Parties.Add(p);
                                break;
                            }
                        }

                        if (team.AllPlayers.Count() == teamSize)
                        {
                            break;
                        }
                    }

                    if (team.AllPlayers.Count() == teamSize)
                    {
                        game.Teams.Add(team);


                    }
                }

                if (game.Teams.Count == teamCount && game.Teams.All(t => t.AllPlayers.Count() == teamSize))
                {
                    gameFound = true;
                    results.Games.Add(game);

                    foreach (var party in game.AllParties)
                    {
                        parties.Remove(party);
                    }


                }

            }
            while (gameFound);


            foreach (var party in results.Games.SelectMany(g => g.AllParties))
            {
                gameFinderContext.WaitingParties.Remove(party);
            }

            return Task.FromResult(results);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> GetMetrics()
        {
            return new Dictionary<string, int>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config"></param>
        public void RefreshConfig(dynamic config)
        {
            QuickQueueOptions options = config.ToObject<QuickQueueOptions>();
            teamSize = options.teamSize;
            teamCount = options.teamCount;
        }
    }
}
