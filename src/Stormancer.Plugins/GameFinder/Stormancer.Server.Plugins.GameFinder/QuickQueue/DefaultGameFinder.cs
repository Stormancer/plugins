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
using Stormancer.Core;
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

    /// <summary>
    /// Base configuration of game sessions created by finders.
    /// </summary>
    public class BaseGameSessionConfig
    {
        /// <summary>
        /// Game launch arguments, passed to the game session.
        /// </summary>
        public Dictionary<string, string> Args { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Configuration of a game session created by the finder.
    /// </summary>
    public class QuickQueueGameSessionConfig : BaseGameSessionConfig
    {
        /// <summary>
        /// Gets or sets the size of teams in the session.
        /// </summary>
        public int TeamSize { get; set; }

        /// <summary>
        /// Gets or sets the number of teams in the session.
        /// </summary>
        public int TeamCount { get; set; }

        /// <summary>
        /// Does the session allows for joining during gameplay ?
        /// </summary>
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

        internal async Task<IEnumerable<Document<QuickQueueGameSessionData>>> QueryGameSessions(ParametersGroup parameters)
        {
            var docs = (await search.QueryAsync<QuickQueueGameSessionData>("gamesessions.quickQueue", JObject.FromObject(new
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

            }), 0, 20, CancellationToken.None)).Hits;

            return docs;
        }

        abstract internal Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IDependencyResolver resolver, IEnumerable<Party> parties);

        /// <summary>
        /// Override to customize if 2 parties can play together in the same game.
        /// </summary>
        /// <param name="resolver"></param>
        /// <param name="p"></param>
        /// <param name="pivot"></param>
        /// <returns></returns>
        abstract protected Task<bool> CanPlayTogether(IDependencyResolver resolver, Party p, Party pivot);

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


            var partyGroups = await GetGroups(gameFinderContext.Scope, gameFinderContext.WaitingParties);


            foreach (var group in partyGroups)
            {
                var teamCount = group.Key.TeamCount;
                var teamSize = group.Key.TeamSize;
                var parties = group.ToList();

                if (group.Key.AllowJoinGameInProgress)
                {
                    var sessions = (await QueryGameSessions(group.Key)).OrderBy(session => session.Source?.CreatedOn).ToList();



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
                                    foreach (var team in (IEnumerable<QuickQueueGameSessionTeamData>?)(session.Source?.Teams) ?? Array.Empty<QuickQueueGameSessionTeamData>())
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
                                                    results.Games.Add(game);
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

                                    //can I create new team ?
                                    if (party.Players.Count <= teamSize && session.Source.TargetTeamCount > session.Source.Teams.Count)
                                    {
                                        var team = new Team(party, null);
                                        var reservation = await gameSessions.CreateReservation(session.Id, team, new JObject(), CancellationToken.None);

                                        if (reservation != null)
                                        {
                                            session.Source.Teams.Add(new QuickQueueGameSessionTeamData { PlayerCount = party.Players.Count, TeamId = team.TeamId });

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
                                                results.Games.Add(game);
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
                                {
                                    //No session found that can contain the party. Create a new one.
                                    var game = new NewGame();
                                    var config = new QuickQueueGameSessionConfig { AllowJoinExistingGame = true, TeamCount = (int)teamCount, TeamSize = (int)teamSize };

                                    var team = new Team(party);
                                    game.Teams.Add(team);

                                    OnCreatingGameSession(gameFinderContext.Scope, config, game);

                                    game.PrivateCustomData.Merge(JObject.FromObject(config));

                                    results.Games.Add(game);
                                    var data = new QuickQueueGameSessionData { CreatedOn = DateTime.UtcNow, TargetTeamSize = (int)teamSize, TargetTeamCount = (int)teamCount };
                                    data.Teams = new List<QuickQueueGameSessionTeamData> { new QuickQueueGameSessionTeamData { PlayerCount = party.Players.Count, TeamId = team.TeamId } };
                                    sessions.Add(new Document<QuickQueueGameSessionData>(game.Id, data) { Version = 1 });
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
                            var team = new Models.Team() { TeamId = teamId.ToString() };


                            for (int pivotId = 0; pivotId < parties.Count; pivotId++)
                            {
                                var pivot = parties[pivotId];

                                if (game.AllParties.Contains(pivot))
                                {
                                    continue;
                                }
                                foreach (var p in game.AllParties)
                                {
                                    if (!await CanPlayTogether(gameFinderContext.Scope, p, pivot))
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

        /// <summary>
        /// Called whenever a new game is created.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="config"></param>
        /// <param name="game"></param>
        protected abstract void OnCreatingGameSession(IDependencyResolver scope, QuickQueueGameSessionConfig config, NewGame game);
    }

    /// <summary>
    /// Quick matchmaking queue
    /// </summary>
    public class QuickQueueGameFinder : QuickQueueGameFinderBase, IGameFinderAlgorithm
    {
        private Func<IDependencyResolver, Party, uint> teamSize = default!;
        private Func<IDependencyResolver, Party, uint> teamCount = default!;
        private Func<IDependencyResolver, Party, Party, bool> canMatch = default!;
        private Func<IDependencyResolver, Party, bool> allowJoinGameInProgress = default!;
        private Action<IDependencyResolver, QuickQueueGameSessionConfig, NewGame> onCreatingGame = default!;


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

        internal override async Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IDependencyResolver resolver, IEnumerable<Party> parties)
        {
            await Task.WhenAll(parties.Select(async p => new { party = p }));
            return parties.OrderByDescending(p => p.Players.Count).GroupBy(p =>
            {


                return new ParametersGroup { TeamCount = teamCount(resolver, p), TeamSize = teamSize(resolver, p), AllowJoinGameInProgress = allowJoinGameInProgress(resolver, p) };

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

        ///<inheritdoc/>
        override protected async Task<bool> CanPlayTogether(IDependencyResolver resolver, Party p, Party pivot)
        {

            return canMatch(resolver, p, pivot);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> GetMetrics()
        {
            return new Dictionary<string, int>();
        }

        ///<inheritdoc/>
        public void RefreshConfig(string id, dynamic config)
        {
            var options = QuickQueueExtensions.GetOptions<QuickQueueOptions>(id);

            teamSize = options.teamSize;
            teamCount = options.teamCount;
            canMatch = options.CanPlayTogether;
            onCreatingGame = options.onCreatingGame;
            allowJoinGameInProgress = options.allowJoinExistingGame;
        }

        ///<inheritdoc/>
        protected override void OnCreatingGameSession(IDependencyResolver scope, QuickQueueGameSessionConfig config, NewGame game)
        {
            onCreatingGame(scope, config, game);
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
        private Func<IDependencyResolver, Party, TPartySettings?, uint> teamSize = default!;
        private Func<IDependencyResolver, Party, TPartySettings?, uint> teamCount = default!;
        private Func<IDependencyResolver, Party, TPartySettings?, Party, TPartySettings?, bool> canPlayTogether = default!;
        Func<IDependencyResolver, Party, Task<TPartySettings?>> getSettings = default!;
        private Func<IDependencyResolver, Party, TPartySettings?, bool> allowJoinGameInProgress = default!;
        private Action<IDependencyResolver, QuickQueueGameSessionConfig, NewGame> onCreatingGame = default!;

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


        private async Task<TPartySettings?> GetOrCreateSettings(IDependencyResolver scope, Party p)
        {
            TPartySettings? settings = default;
            if (!p.CacheStorage.TryGetValue("matchmaking.settings", out var obj))
            {

                settings = await getSettings(scope, p);
                p.CacheStorage.Add("matchmaking.settings", settings!);
            }
            else
            {
                settings = (TPartySettings)obj;
            }

            return settings;
        }

        override protected async Task<bool> CanPlayTogether(IDependencyResolver scope, Party p, Party pivot)
        {
            return canPlayTogether(scope, p, await GetOrCreateSettings(scope, p), p, await GetOrCreateSettings(scope, pivot));
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
            onCreatingGame = options.onCreatingGame;
        }

        internal override async Task<IEnumerable<IGrouping<ParametersGroup, Party>>> GetGroups(IDependencyResolver scope, IEnumerable<Party> parties)
        {
            var p2 = await Task.WhenAll(parties.Select(async p => new { party = p, settings = await GetOrCreateSettings(scope, p) }));


            return p2.OrderByDescending(p => p.party.Players.Count).GroupBy(p =>
            {

                TPartySettings? settings = p.settings;

                var group = new ParametersGroup { TeamCount = teamCount(scope, p.party, settings), TeamSize = teamSize(scope, p.party, settings), AllowJoinGameInProgress = allowJoinGameInProgress(scope, p.party, settings) };
                return group;
            }, p => p.party);
        }

        ///<inheritdoc/>
        protected override void OnCreatingGameSession(IDependencyResolver scope, QuickQueueGameSessionConfig config, NewGame game)
        {
            onCreatingGame(scope, config, game);
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
        public int TargetTeamSize { get; set; }
    }
}
