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
using Newtonsoft.Json;
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
        private Func<dynamic?, uint> teamSize = default!;
        private Func<dynamic?, uint> teamCount = default!;
        private Func<dynamic?, dynamic?, bool> canMatch = default!;
        private Func<Party, Task<dynamic?>> getSettings = default!;
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
        public async Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
        {
            var results = new GameFinderResult();
            if (!gameFinderContext.WaitingParties.Any())
            {
                return results;

            }
            var changeOccured = false;
            await Task.WhenAll(gameFinderContext.WaitingParties.Select(async p => new { party = p, settings = await GetOrCreateSettings(p) }));
            var partyGroups = gameFinderContext.WaitingParties.OrderByDescending(p => p.Players.Count).GroupBy(p =>
            {
                dynamic? settings = GetOrCreateSettings(p).Result;

                return new ParametersGroup { TeamCount = teamCount(settings), TeamSize = teamSize(settings) };

            });

            foreach (var group in partyGroups)
            {
                var teamCount = group.Key.TeamCount;
                var teamSize = group.Key.TeamSize;
                var parties = group.ToList();
                do
                {

                    changeOccured = false;

                    var game = new NewGame();
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

                                foreach (var p in game.AllParties)
                                {
                                    if (!await CanPlayTogether(p, pivot))
                                    {
                                        continue;
                                    }
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

                        results.Games.Add(game);




                    }
                    foreach (var party in game.AllParties)
                    {
                        changeOccured = true;
                        parties.Remove(party);
                    }

                }
                while (changeOccured);
            }

            foreach (var party in results.Games.SelectMany(g => g.AllParties()))
            {
                gameFinderContext.WaitingParties.Remove(party);
            }

            return results;
        }

        private async Task<bool> CanPlayTogether(Party p, Party pivot)
        {

            return canMatch(await GetOrCreateSettings(p), await GetOrCreateSettings(pivot));
        }

        private async Task<dynamic?> GetOrCreateSettings(Party p)
        {
            dynamic? settings;
            if (!p.CacheStorage.TryGetValue("matchmaking.settings", out settings))
            {

                settings = await getSettings(p);
                p.CacheStorage.Add("matchmaking.settings", settings);
            }
            return settings;
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
        /// <param name="id"></param>
        /// <param name="config"></param>
        public void RefreshConfig(string id, dynamic config)
        {
            var options = QuickQueueExtensions.GetOptions<QuickQueueOptions>(id);

            teamSize = options.teamSize;
            teamCount = options.teamCount;
            canMatch = options.CanPlayTogether;
            getSettings = options.getSettings;
        }
    }

    internal struct ParametersGroup : IEquatable<ParametersGroup>
    {
        public uint TeamSize { get; set; }
        public uint TeamCount { get; set; }

        public bool Equals(ParametersGroup other)
        {
            return TeamSize == other.TeamSize && TeamCount == other.TeamCount;
        }
    }

    /// <summary>
    /// Quick matchmaking queue
    /// </summary>
    public class QuickQueueGameFinder<TPartySettings> : IGameFinderAlgorithm
    {
        private Func<TPartySettings?, uint> teamSize = default!;
        private Func<TPartySettings?, uint> teamCount = default!;
        private Func<TPartySettings?, TPartySettings?, bool> canPlayTogether = default!;
        Func<Party, Task<TPartySettings?>> getSettings = default!;
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
        public async Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
        {
            var results = new GameFinderResult();
            if (!gameFinderContext.WaitingParties.Any())
            {
                return results;

            }

            var changeOccured = false;
            await Task.WhenAll(gameFinderContext.WaitingParties.Select(async p => new { party = p, settings = await GetOrCreateSettings(p) }));
            var partyGroups = gameFinderContext.WaitingParties.OrderByDescending(p => p.Players.Count).GroupBy(p =>
            {
                TPartySettings? settings = GetOrCreateSettings(p).Result;

                return new ParametersGroup { TeamCount = teamCount(settings), TeamSize = teamSize(settings) };

            });

            foreach (var group in partyGroups)
            {
                var teamCount = group.Key.TeamCount;
                var teamSize = group.Key.TeamSize;
                var parties = group.ToList();
                do
                {


                    changeOccured = false;

                    var game = new NewGame();
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
                            foreach (var p in game.AllParties)
                            {
                                if (!await CanPlayTogether(p, pivot))
                                {
                                    continue;
                                }
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

                        results.Games.Add(game);

                    }

                    foreach (var party in game.AllParties)
                    {
                        parties.Remove(party);
                        changeOccured = true;
                    }
                }
                while (changeOccured);
            }


            foreach (var party in results.Games.SelectMany(g => g.AllParties()))
            {
                gameFinderContext.WaitingParties.Remove(party);
            }

            return results;
        }

        private async Task<TPartySettings?> GetOrCreateSettings(Party p)
        {
            TPartySettings? settings = default;
            if (!p.CacheStorage.TryGetValue("matchmaking.settings", out var obj))
            {

                settings = await getSettings(p);
                p.CacheStorage.Add("matchmaking.settings", settings!);
            }
            else
            {
                settings = (TPartySettings)obj;
            }

            return settings;
        }

        private async Task<bool> CanPlayTogether(Party p, Party pivot)
        {
            return canPlayTogether(await GetOrCreateSettings(p), await GetOrCreateSettings(pivot));
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
        /// <param name="id"></param>
        /// <param name="config"></param>
        public void RefreshConfig(string id, dynamic config)
        {
            var options = QuickQueueExtensions.GetOptions<QuickQueueOptions<TPartySettings>>(id);

            teamSize = options.teamSize;
            teamCount = options.teamCount;
            canPlayTogether = options.canPlayTogether;
            getSettings = options.GetSettings;
        }
    }
}
