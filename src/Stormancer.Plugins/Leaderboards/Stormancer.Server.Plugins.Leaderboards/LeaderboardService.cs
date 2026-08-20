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

//using Jose;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    /// <summary>
    /// Leaderboard constants
    /// </summary>
    public class LeaderboardConstants
    {
        /// <summary>
        /// Id of the policy used to generate leaderboard index names.
        /// </summary>
        public const string INDEX_ID = "leaderboards";
    }
    class LeaderboardService : ILeaderboardService
    {
        private readonly ILogger _logger;        
        private readonly IUserService _userService;
        private Func<IEnumerable<ILeaderboardEventHandler>> eventHandlers;
        private readonly IFriendsService friendsService;
        private readonly ILeaderboardStorage _storage;

        /// <summary>
        /// True if the leaderboards treats exequo as same rank. False if they are ordered by ascending creation date.
        /// </summary>
        public bool EnableExaequo { get; set; } = false;

        public LeaderboardService(
            ILogger logger,
            Func<IEnumerable<ILeaderboardEventHandler>> eventHandlers,
            IFriendsService friendsService,
            IConfiguration configuration,
            IUserService userService,
            ILeaderboardStorage storage)
        {
            _logger = logger;            
            _userService = userService;
            this.eventHandlers = eventHandlers;
            this.friendsService = friendsService;
            _storage = storage;

            if (LeaderboardContinuationQuery.Key == null)
            {
                LeaderboardContinuationQuery.Key = new Lazy<byte[]>(() =>
                {
                    var key = configuration.GetValue<string?>("security.tokenKey", null);
                    if (key == null)
                    {
                        var bytes = new byte[32];
                        System.Security.Cryptography.RandomNumberGenerator.Fill(new Span<byte>(bytes));
                        return bytes;
                    }
                    else
                    {
                        return System.Convert.FromBase64String(key);
                    }
                });
            }

        }        

        public string GetIndex(string leaderboardName)
        {
            return _storage.GetIndex(leaderboardName);
        }

        public async Task<ScoreRecord?> GetScore(string playerId, string leaderboardName)
        {
            return await _storage.GetScore(leaderboardName, playerId);            
        }

        public async Task<Dictionary<string, ScoreRecord?>> GetScores(IEnumerable<string> playerIds, string leaderboardName)
        {
            return await _storage.GetScores(leaderboardName, playerIds);

            
        }

        public async Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken)
        {
            await AdjustQuery(filters, cancellationToken);


            return await _storage.GetRanking(score, filters, leaderboardName, EnableExaequo, cancellationToken);

            
        }

        public async Task<long> GetTotal(LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken)
        {
            await AdjustQuery(filters, cancellationToken);


            return await _storage.GetTotal(leaderboardName, filters, cancellationToken);
        }

        public async Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery leaderboardQuery, CancellationToken cancellationToken)
        {
            await AdjustQuery(leaderboardQuery, cancellationToken);
            if (string.IsNullOrEmpty(leaderboardQuery.ScorePath))
            {
                throw new ArgumentNullException("ScorePath");
            }
            if (leaderboardQuery.Size <= 0)
            {
                leaderboardQuery.Size = 10;
            }          

            var leaderboardResult =  await _storage.Query(leaderboardQuery, EnableExaequo, cancellationToken);

            var ctx = new QueryResponseCtx(leaderboardQuery, leaderboardResult);

            await eventHandlers().RunEventHandler(eh => eh.OnQueryResponse(ctx), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running QueryResponse event handlers", ex));
            return leaderboardResult;
        }

        private ValueTask AdjustQuery(LeaderboardQuery leaderboardQuery, CancellationToken cancellationToken)
        {
            if (leaderboardQuery.Adjusted)
            {
                return ValueTask.CompletedTask;
            }
            else
            {
                return AdjustQueryImpl(leaderboardQuery, cancellationToken);
            }
            async ValueTask AdjustQueryImpl(LeaderboardQuery leaderboardQuery, CancellationToken cancellationToken)
            {
                leaderboardQuery.Adjusted = true;
                await this.eventHandlers().RunEventHandler(eh => eh.OnQueryingLeaderboard(leaderboardQuery), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running OnQueryingLeaderboard event handlers", ex));

                if (leaderboardQuery.FriendsOnly)
                {
                    if (string.IsNullOrEmpty(leaderboardQuery.UserId))
                    {
                        throw new InvalidOperationException("LeaderboardQuery.UserId must be set if LeaderboardQuery.FriendsOnly is set.");
                    }
                    var friends = await friendsService.GetFriends(leaderboardQuery.UserId, cancellationToken);
                    var friendIds = friends.Select(f => f.TryGetIdForPlatform(Users.Constants.PROVIDER_TYPE_STORMANCER, out var userId) ? userId : null).WhereNotNull();
                    if (leaderboardQuery.FriendsIds != null && leaderboardQuery.FriendsIds.Any())
                    {
                        leaderboardQuery.FilteredUserIds = leaderboardQuery.FriendsIds.Intersect(friendIds);
                    }
                    else
                    {
                        leaderboardQuery.FilteredUserIds = friendIds;
                    }
                    if (!leaderboardQuery.FilteredUserIds.Contains(leaderboardQuery.UserId))
                    {
                        var list = leaderboardQuery.FilteredUserIds.ToList();
                        list.Add(leaderboardQuery.UserId);
                        leaderboardQuery.FilteredUserIds = list;
                    }
                }
            }
        }

        public Task<LeaderboardResult<ScoreRecord>> QueryCursor(string cursor, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                throw new ClientException("Invalid continuation: no more results available");
            }
            var query = LeaderboardContinuationQuery.DeserializeContinuationQuery(cursor);

            return Query(query, cancellationToken);
        }

        public Task UpdateScore(string id, string leaderboardName, Func<ScoreRecord?, Task<ScoreRecord>> updater)
        {
            return UpdateScores(Enumerable.Repeat(new LeaderboardEntryId(leaderboardName, id), 1), (i, old) => updater(old));
        }

        //private string GetDocumentId(string leaderboardName, string id)
        //{
        //    return $"{leaderboardName}#{id}";
        //}

        

        private string GetDocumentId(string leaderboardName, string id)
        {
            return $"{leaderboardName}#{id}";
        }

        public async Task UpdateScores(IEnumerable<LeaderboardEntryId> ids, Func<LeaderboardEntryId, ScoreRecord?, Task<ScoreRecord>> scoreUpdater)
        {
            var results = ids.ToDictionary(id => id, id => false);

            async Task<Dictionary<LeaderboardEntryId, ScoreUpdate>> UpdateRecords(IReadOnlyDictionary<LeaderboardEntryId, ScoreRecord> recordsById)
            {
                var updates = new Dictionary<LeaderboardEntryId, ScoreUpdate>();


                foreach (var kvp in recordsById)
                {
                    var currentScore = kvp.Value;
                    var id = kvp.Key;

                    var r = currentScore != null ? new ScoreRecord
                    {
                        CreatedOn = currentScore.CreatedOn,
                        Id = currentScore.Id,
                        LeaderboardName = currentScore.LeaderboardName,
                        Document = (JObject)currentScore.Document.DeepClone(),
                        Scores = (JObject)currentScore.Scores.DeepClone()
                    } : null;

                    var score = await scoreUpdater(id, r);
                    if (score != null)
                    {
                        score.Id = id.Id;
                        score.LeaderboardName = id.LeaderboardName;
                    }

                    var updated = false;
                    if (currentScore == null && score != null)
                    {
                        updated = true;
                        score.CreatedOn = DateTime.UtcNow;
                    }
                    else if (currentScore != null && score == null)
                    {
                        updated = true;

                    }
                    else if (score != null && currentScore != null && !JToken.DeepEquals(score.Scores, currentScore.Scores))
                    {
                        updated = true;
                        // Update the date only if the score has changed
                        score.CreatedOn = DateTime.UtcNow;
                    }
                    else if (score != null && currentScore != null && !JToken.DeepEquals(score.Document, currentScore.Document))
                    {
                        updated = true;
                    }

                    if (updated)
                    {
                        updates.Add(id, new ScoreUpdate { OldValue = currentScore, NewValue = score! });
                    }
                    else//No need to update
                    {
                        results[id] = true;
                    }
                }

                var ctx = new UpdatingScoreCtx(updates.Values);
                await eventHandlers().RunEventHandler(e => e.UpdatingScores(ctx), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running leaderboard.UpdatingScore event handler", ex));
                return updates;
            }

            await _storage.UpdateScores(results, UpdateRecords);            
        }

        public async Task RemoveLeaderboardEntry(string leaderboardName, string entryId)
        {
            await _storage.RemoveEntry(leaderboardName, entryId);            
        }

        public async Task ClearAllScores()
        {
            await eventHandlers().RunEventHandler(eh => eh.ClearAllScores(), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running leaderboards clear all scores event handlers", ex));

            await _storage.ClearAllScores();

            
        }

        public async Task ClearAllScores(string leaderboardName)
        {
            await eventHandlers().RunEventHandler(eh => eh.ClearAllScores(leaderboardName), ex => _logger.Log(LogLevel.Error, "leaderboard", $"An error occured while running leaderboards clear all scores event handlers for leaderboard {leaderboardName}", ex));

            await _storage.ClearAllScores(leaderboardName);            
        }

        public async Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards()
        {
            return await _storage.GetQuickAccessLeaderboards();            
        }

        public async Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard)
        {
            await _storage.AddQuickAccessLeaderboard(leaderboard);
        }

        public async Task RemoveQuickAccessLeaderboard(string leaderboardName)
        {
            await _storage.RemoveQuickAccessLeaderboard(leaderboardName);            
        }

   
    }
}
