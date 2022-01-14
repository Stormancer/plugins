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
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Models;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    internal static class QuickQueueConstants
    {
        public const string QUICKQUEUE_GAMESESSIONS_INDEX = "gamesessions.quickQueue";
        public const string GAMESESSION_CONFIG_PATH = "quickqueue";
    }
    public class QuickQueueGameSessionConfig
    {
        public int TeamSize { get; set; }
        public int TeamCount { get; set; }
        public bool AllowJoinExistingGame { get; set; }
    }


    public abstract class QuickQueueGameFinderBase
    {
        protected readonly SearchEngine search;
        protected readonly IGameSessions gameSessions;

        public QuickQueueGameFinderBase(SearchEngine search, IGameSessions gameSessions)
        {
            this.search = search;
            this.gameSessions = gameSessions;
        }

        internal ValueTask<List<Document<QuickQueueGameSessionData>>> QueryGameSessions(ParametersGroup parameters)
        {
            return search.QueryAsync<QuickQueueGameSessionData>("gamesessions.quickQueue", JObject.FromObject(new
            {
                @bool = new
                {
                    must = new[]
                       {
                                    new
                                    {
                                        match = new
                                        {
                                            field = "targetTeamCount",
                                            value = parameters.TeamCount
                                        }
                                    },
                                    new
                                    {
                                        match = new
                                        {
                                            field = "targetTeamSize",
                                            value = parameters.TeamSize
                                        }
                                    }
                                }
                }

            }), 20, CancellationToken.None).ToListAsync();
        }

        abstract internal Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IEnumerable<Party> parties);
        abstract protected Task<bool> CanPlayTogether(Party p, Party pivot);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        protected async Task<GameFinderResult> FindGamesImpl(GameFinderContext gameFinderContext)
        {
            var results = new GameFinderResult();
            if (!gameFinderContext.WaitingParties.Any())
            {
                return results;

            }

            var changeOccured = false;
           

            var partyGroups = await GetGroups(gameFinderContext.WaitingParties);


            foreach (var group in partyGroups)
            {
                var teamCount = group.Key.TeamCount;
                var teamSize = group.Key.TeamSize;
                var parties = group.ToList();

                if (group.Key.AllowJoinGameInProgress)
                {
                    var sessions = (await QueryGameSessions(group.Key)).OrderBy(session => session.Source.CreatedOn).ToList();



                    var p = group.ToList();
                    while (p.Any())
                    {
                        changeOccured = false;
                        var party = p.FirstOrDefault();
                        if (party != null)
                        {
                            async Task<List<Document<QuickQueueGameSessionData>>> ProcessParty(List<Document<QuickQueueGameSessionData>> sessions, Party party)
                            {
                                foreach (var session in sessions)
                                {
                                    foreach (var team in session.Source.Teams)
                                    {
                                        if (team.PlayerCount + party.Players.Count <= teamSize)
                                        {
                                            var reservation = await gameSessions.CreateReservation(session.Id, new Team(party) { TeamId = team.TeamId }, new JObject(), CancellationToken.None);

                                            if (reservation != null)
                                            {
                                                team.PlayerCount += party.Players.Count;

                                                //Add game to result
                                                var game = results.Games.FirstOrDefault(g => g.Id == session.Id);
                                                if (game != null)
                                                {
                                                    var gameTeam = game.Teams.FirstOrDefault(t => t.TeamId == team.TeamId);
                                                    if (gameTeam != null)
                                                    {
                                                        gameTeam.Parties.Add(party);
                                                    }
                                                    else
                                                    {
                                                        game.Teams.Add(new Team(party) { TeamId = team.TeamId });
                                                    }
                                                }
                                                else
                                                {
                                                    game = new ExistingGame(session.Id);
                                                    game.Teams.Add(new Team(party) { TeamId = team.TeamId });
                                                }
                                                p.Remove(party);
                                                return sessions;
                                            }
                                            else
                                            {
                                                //We should have been able to do a reservation. As we didn't, we need to retry.
                                                return (await QueryGameSessions(group.Key)).OrderBy(session => session.Source.CreatedOn).ToList();


                                            }

                                        }
                                    }
                                }
                                {
                                    //No session found that can contain the party. Create a new one.
                                    var game = new NewGame();
                                    game.PrivateCustomData = JObject.FromObject(new QuickQueueGameSessionConfig { AllowJoinExistingGame = true, TeamCount = (int)teamCount, TeamSize = (int)teamSize });
                                    var team = new Team(party);
                                    game.Teams.Add(team);
                                    results.Games.Add(game);
                                    var data = new QuickQueueGameSessionData { CreatedOn = DateTime.UtcNow, TargetPlayerCount = (int)(teamSize * teamCount), TargetTeamCount = (int)teamCount };
                                    data.Teams = new List<QuickQueueGameSessionTeamData> { new QuickQueueGameSessionTeamData { PlayerCount = party.Players.Count, TeamId = team.TeamId } };
                                    sessions.Add(new Document<QuickQueueGameSessionData> { Id = game.Id, Source = data });
                                    p.Remove(party);
                                    return sessions;
                                }
                            }

                            sessions = await ProcessParty(sessions, party);

                        }


                    }

                }
                else
                {
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

            }


            foreach (var party in results.Games.SelectMany(g => g.AllParties()))
            {
                gameFinderContext.WaitingParties.Remove(party);
            }

            return results;
        }
    }

    /// <summary>
    /// Quick matchmaking queue
    /// </summary>
    public class QuickQueueGameFinder : QuickQueueGameFinderBase, IGameFinderAlgorithm
    {
        private Func<dynamic?, uint> teamSize = default!;
        private Func<dynamic?, uint> teamCount = default!;
        private Func<dynamic?, dynamic?, bool> canMatch = default!;
        private Func<Party, Task<dynamic?>> getSettings = default!;
        private Func<dynamic?, bool> allowJoinGameInProgress = default!;


        /// <summary>
        /// Creates a new QuickQueueGameFinder instance.
        /// </summary>
        /// <param name="search"></param>
        /// <param name="gameSessions"></param>
        public QuickQueueGameFinder(SearchEngine search, IGameSessions gameSessions) : base(search, gameSessions)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        public JObject ComputeDataAnalytics(GameFinderContext gameFinderContext)
        {
            return new JObject();
        }

        internal override async Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IEnumerable<Party> parties)
        {
            await Task.WhenAll(parties.Select(async p => new { party = p, settings = await GetOrCreateSettings(p) }));
            return parties.OrderByDescending(p => p.Players.Count).GroupBy(p =>
            {
                dynamic? settings = GetOrCreateSettings(p).Result;

                return new ParametersGroup { TeamCount = teamCount(settings), TeamSize = teamSize(settings), AllowJoinGameInProgress = allowJoinGameInProgress(settings) };

            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        public Task<GameFinderResult> FindGames(GameFinderContext gameFinderContext)
        {
            return FindGamesImpl(gameFinderContext);
        }

        override protected async Task<bool> CanPlayTogether(Party p, Party pivot)
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
        public bool AllowJoinGameInProgress { get; set; }

        public bool Equals(ParametersGroup other)
        {
            return TeamSize == other.TeamSize && TeamCount == other.TeamCount && AllowJoinGameInProgress == other.AllowJoinGameInProgress;
        }
    }

    /// <summary>
    /// Quick matchmaking queue
    /// </summary>
    public class QuickQueueGameFinder<TPartySettings> : QuickQueueGameFinderBase, IGameFinderAlgorithm
    {
        private Func<TPartySettings?, uint> teamSize = default!;
        private Func<TPartySettings?, uint> teamCount = default!;
        private Func<TPartySettings?, TPartySettings?, bool> canPlayTogether = default!;
        Func<Party, Task<TPartySettings?>> getSettings = default!;
        private Func<TPartySettings?, bool> allowJoinGameInProgress = default!;


        /// <summary>
        /// Creates a new QuickQueueGameFinder instance
        /// </summary>
        /// <param name="search"></param>
        /// <param name="gameSessions"></param>
        public QuickQueueGameFinder(SearchEngine search, IGameSessions gameSessions) : base(search, gameSessions)
        {

        }

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
            return FindGamesImpl(gameFinderContext);
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

        override protected async Task<bool> CanPlayTogether(Party p, Party pivot)
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
            allowJoinGameInProgress = options.allowJoinExistingGame;
        }

        internal override async Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IEnumerable<Party> parties)
        {
            await Task.WhenAll(parties.Select(async p => new { party = p, settings = await GetOrCreateSettings(p) }));


            return parties.OrderByDescending(p => p.Players.Count).GroupBy(p =>
            {
                TPartySettings? settings = GetOrCreateSettings(p).Result;

                var group = new ParametersGroup { TeamCount = teamCount(settings), TeamSize = teamSize(settings), AllowJoinGameInProgress = allowJoinGameInProgress(settings) };
                return group;
            });
        }
    }

    public class QuickQueueGameSessionTeamData
    {
        public string TeamId { get; set; }
        public int PlayerCount { get; set; }
    }
    /// <summary>
    /// Data associated with a gameSession
    /// </summary>
    public class QuickQueueGameSessionData
    {
        public DateTime CreatedOn { get; set; }

        public List<QuickQueueGameSessionTeamData> Teams { get; set; }

        public int TargetTeamCount { get; set; }
        public int TargetPlayerCount { get; set; }
    }
}
