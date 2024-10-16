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
        private readonly IESClientFactory _clientFactory;
        private readonly IUserService _userService;
        private Func<IEnumerable<ILeaderboardEventHandler>> eventHandlers;
        private Func<IEnumerable<ILeaderboardIndexMapping>> leaderboardIndexMapping;
        private readonly IFriendsService friendsService;

        /// <summary>
        /// True if the leaderboards treats exequo as same rank. False if they are ordered by ascending creation date.
        /// </summary>
        public bool EnableExequo { get; set; } = false;

        public LeaderboardService(
            ILogger logger,
            IESClientFactory clientFactory,
            Func<IEnumerable<ILeaderboardEventHandler>> eventHandlers,
            Func<IEnumerable<ILeaderboardIndexMapping>> leaderboardIndexMapping,
            IFriendsService friendsService,
            IConfiguration configuration,
            IUserService userService)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _userService = userService;
            this.eventHandlers = eventHandlers;
            this.leaderboardIndexMapping = leaderboardIndexMapping;
            this.friendsService = friendsService;

            if (_key == null)
            {
                _key = new Lazy<byte[]>(() =>
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

        private string GetModifiedLeaderboardName(string leaderboardName)
        {
            foreach (var mapper in leaderboardIndexMapping())
            {
                var mapping = mapper.GetIndex(leaderboardName);
                if (!string.IsNullOrEmpty(mapping))
                {
                    return mapping;
                }
            }
            return leaderboardName;
        }

        public async Task<Nest.IElasticClient> CreateESClient<T>(params object[] parameters)
        {
            var result = await _clientFactory.CreateClient<T>(LeaderboardConstants.INDEX_ID, parameters);


            return result;
        }

        public string GetIndex(string leaderboardName)
        {
            return _clientFactory.GetIndex<ScoreRecord>(LeaderboardConstants.INDEX_ID, GetModifiedLeaderboardName(leaderboardName));

        }

        private double GetValue(ScoreRecord record, string scorePath)
        {
            if (string.IsNullOrEmpty(scorePath))
            {
                throw new ArgumentException($"scorePath must be non null nor empty", nameof(scorePath));
            }
            JToken current = record.Scores;
            foreach (var segment in scorePath.Split('.'))
            {
                current = current[segment] ?? throw new ArgumentException($"Path {scorePath} does not exist in score.");
            }
            return current.ToObject<double>();
        }

        public Nest.QueryContainer CreatePreviousPaginationFilter(Nest.QueryContainerDescriptor<ScoreRecord> q, ScoreRecord pivot, string path, LeaderboardOrdering leaderboardOrdering)
        {
            var pivotScore = GetValue(pivot, path);
            var fullScorePath = "scores." + path;
            // descending : ( score > pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.Id AND Id < pivot.Id) 
            // ascending :  ( score < pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.Id AND Id > pivot.Id) 
            return q.Bool(
                b1 => b1.Should(
                    q1 => q1.Range(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(fullScorePath).GreaterThan(pivotScore) : r.Field(fullScorePath).LessThan(pivotScore)),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(record => record.CreatedOn).LessThan(pivot.CreatedOn) : r.Field(record => record.CreatedOn).GreaterThan(pivot.CreatedOn))
                        )
                    ),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThanOrEquals(pivot.CreatedOn).GreaterThanOrEquals(pivot.CreatedOn)),
                            q2 => q2.TermRange(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(record => record.Id).LessThan(pivot.Id) : r.Field(record => record.Id).GreaterThan(pivot.Id))
                        )
                    )
                )
            );
        }

        public Nest.QueryContainer CreateNextPaginationFilter(Nest.QueryContainerDescriptor<ScoreRecord> q, ScoreRecord pivot, string path, LeaderboardOrdering leaderboardOrdering)
        {
            var pivotScore = GetValue(pivot, path);
            var fullScorePath = "scores." + path;

            // descending : ( score < pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.Id AND Id > pivot.Id) 
            // ascending :  ( score > pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.Id AND Id < pivot.Id) 
            return q.Bool(
                b1 => b1.Should(
                    q1 => q1.Range(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(fullScorePath).LessThan(pivotScore) : r.Field(fullScorePath).GreaterThan(pivotScore)),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(record => record.CreatedOn).GreaterThan(pivot.CreatedOn) : r.Field(record => record.CreatedOn).LessThan(pivot.CreatedOn))
                        )
                    ),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThanOrEquals(pivot.CreatedOn).GreaterThanOrEquals(pivot.CreatedOn)),
                            q2 => q2.TermRange(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(record => record.Id).GreaterThan(pivot.Id) : r.Field(record => record.Id).LessThan(pivot.Id))
                        )
                    )
                )
            );
        }

        public async Task<ScoreRecord?> GetScore(string playerId, string leaderboardName)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            var startResult = await client.GetAsync<ScoreRecord>(GetDocumentId(index, playerId));
            if (!startResult.Found)
            {
                return null;
            }

            return startResult.Source;
        }

        public async Task<Dictionary<string, ScoreRecord?>> GetScores(IEnumerable<string> playerIds, string leaderboardName)
        {
            var finalResults = playerIds.ToDictionary(id => id, _ => default(ScoreRecord));
            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            var ids = playerIds.Select(id => GetDocumentId(leaderboardName, id));
            var startResult = await client.MultiGetAsync(v => v.GetMany<ScoreRecord>(ids));
            var results = startResult.GetMany<ScoreRecord>(ids);

            foreach (var h in results)
            {
                if (h.Found && finalResults.ContainsKey(h.Source.Id))
                {
                    finalResults[h.Source.Id] = h.Source;
                }
            }
            return finalResults;
        }

        public async Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken)
        {
            await AdjustQuery(filters, cancellationToken);

            var scoreValue = GetValue(score, filters.ScorePath);
            var fullScorePath = "scores." + filters.ScorePath;
            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            var rankResult = await client.CountAsync<ScoreRecord>(desc => desc
                .Query(query =>
                    CreateQuery(query, filters,
                        q =>
                        {
                            var shouldClauses = new List<Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>>();
                            if (filters.Order == LeaderboardOrdering.Descending)
                            {
                                shouldClauses.Add(q1 => q1.Range(r => r.Field(fullScorePath).GreaterThan(scoreValue)));

                                if (!EnableExequo)
                                {
                                    shouldClauses.Add(q1 => q1.Bool(b2 => b2.Must(
                                        q2 => q2.Term(t => t.Field(fullScorePath).Value(scoreValue)),
                                        q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThan(score.CreatedOn))
                                    )));
                                }
                            }
                            else if (filters.Order == LeaderboardOrdering.Ascending)
                            {
                                shouldClauses.Add(q1 => q1.Range(r => r.Field(fullScorePath).LessThan(scoreValue)));

                                if (!EnableExequo)
                                {
                                    shouldClauses.Add(q1 => q1.Bool(b2 => b2.Must(
                                        q2 => q2.Term(t => t.Field(fullScorePath).Value(scoreValue)),
                                        q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).GreaterThan(score.CreatedOn))
                                    )));
                                }
                            }

                            return q.Bool(b => b.Should(shouldClauses));
                        }
                    )
                )
            , cancellationToken);

            if (!rankResult.IsValid)
            {
                throw new InvalidOperationException($"Failed to compute rank. {rankResult.ServerError.Error.Reason}");
            }
            return rankResult.Count + 1;
        }

        public async Task<long> GetTotal(LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken)
        {
            await AdjustQuery(filters, cancellationToken);

            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);

            var rankResult = await client.CountAsync<ScoreRecord>(desc => desc
                    .Query(query =>
                        CreateQuery(query, filters))
                    .IgnoreUnavailable(), cancellationToken);
            if (!rankResult.IsValid)
            {
                throw new InvalidOperationException($"Failed to compute total scores in filter. {rankResult.ServerError.Error.Reason}");
            }
            return rankResult.Count;
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

            var fullScorePath = "scores." + leaderboardQuery.ScorePath;
            var leaderboardContinuationQuery = leaderboardQuery as LeaderboardContinuationQuery;

            var isContinuation = leaderboardContinuationQuery != null;
            var isPreviousContinuation = leaderboardContinuationQuery != null && leaderboardContinuationQuery.IsPrevious;

            var index = GetModifiedLeaderboardName(leaderboardQuery.Name);
            var client = await CreateESClient<ScoreRecord>(index);
            ScoreRecord? start = null;
            if (!string.IsNullOrEmpty(leaderboardQuery.StartId))
            {
                start = await GetScore(leaderboardQuery.StartId, leaderboardQuery.Name);
                if (start == null)
                {
                    return new LeaderboardResult<ScoreRecord>() { LeaderboardName = leaderboardQuery.Name, Total = 0 };
                }
            }

            var result = await client.SearchAsync<ScoreRecord>(s =>
            {
                s = s.AllowNoIndices();
                s = s.Query(query => CreateQuery(query, leaderboardQuery, q =>
                {

                    if (start != null)//If we have a pivot we must add constraint to start the result around it.
                    {
                        //Create next/previous additional constraints
                        if (isPreviousContinuation)
                        {
                            return CreatePreviousPaginationFilter(q, start, leaderboardQuery.ScorePath, leaderboardQuery.Order);
                        }
                        else
                        {
                            return CreateNextPaginationFilter(q, start, leaderboardQuery.ScorePath, leaderboardQuery.Order);
                        }
                    }
                    else
                    {
                        return q;
                    }
                })).AllowNoIndices();

                if (leaderboardQuery.Order == LeaderboardOrdering.Descending)
                {
                    if (isPreviousContinuation)
                    {
                        s = s.Sort(sort => sort.Ascending(fullScorePath).Descending(record => record.CreatedOn));
                    }
                    else
                    {
                        s = s.Sort(sort => sort.Descending(fullScorePath).Ascending(record => record.CreatedOn));
                    }
                }
                else
                {
                    if (isPreviousContinuation)
                    {
                        s = s.Sort(sort => sort.Descending(fullScorePath).Ascending(record => record.CreatedOn));
                    }
                    else
                    {
                        s = s.Sort(sort => sort.Ascending(fullScorePath).Descending(record => record.CreatedOn));
                    }
                }
                if ((isContinuation && !isPreviousContinuation) || start == null)
                {
                    s = s.Size(leaderboardQuery.Size + 1).From(leaderboardQuery.Skip); // We get one more document  than necessary to be able to determine if we can build a "next" continuation
                }
                else // The pivot is not included in the result set, if we are not running a continuation query, we must prefix the results with the pivot.
                {
                    s = s.Size(leaderboardQuery.Size).From(leaderboardQuery.Skip);
                }

                return s;
            }, cancellationToken);

            if (!result.IsValid)
            {
                if (result.ServerError != null)
                {
                    if (result.ServerError.Status == 404)
                    {
                        return new LeaderboardResult<ScoreRecord> { LeaderboardName = leaderboardQuery.Name, Results = new List<LeaderboardRanking<ScoreRecord>>() };
                    }
                    throw new InvalidOperationException($"Failed to query leaderboard : {result.ServerError.Error.Reason}");
                }
                else if (result.OriginalException != null)
                {
                    throw new InvalidOperationException($"Failed to query leaderboard : {result.OriginalException.Message}", result.OriginalException);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to query leaderboard : an unknown error occurred.");
                }
            }
            var documents = result.Documents.ToList();
            if (!isContinuation && start != null)
            {
                documents.Insert(0, start);
            }
            else if ((leaderboardQuery as LeaderboardContinuationQuery)?.IsPrevious == true)
            {
                documents.Reverse();
            }

            var leaderboardResult = new LeaderboardResult<ScoreRecord> { LeaderboardName = leaderboardQuery.Name };
            leaderboardResult.Total = await GetTotal(leaderboardQuery, leaderboardQuery.Name, cancellationToken);

            // Compute rankings
            if (documents.Any())
            {
                int firstRank = 0;
                try
                {
                    firstRank = (int)await GetRanking(documents.First(), leaderboardQuery, leaderboardQuery.Name, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException($"Failed to query leaderboard", ex);
                }
                var rank = firstRank;
                var lastScore = double.MaxValue;
                var lastRank = firstRank;
                var results = new List<LeaderboardRanking<ScoreRecord>>();

                foreach (var doc in documents.Take(leaderboardQuery.Size))
                {
                    //Remove leaderboardName from document.


                    if (EnableExequo)
                    {
                        int currentRank;
                        var v = GetValue(doc, leaderboardQuery.ScorePath);
                        if (v == lastScore)
                        {
                            currentRank = lastRank;

                        }
                        else
                        {
                            currentRank = rank;
                        }

                        results.Add(new LeaderboardRanking<ScoreRecord> { Document = doc, Ranking = currentRank });
                        lastRank = currentRank;
                    }
                    else
                    {
                        results.Add(new LeaderboardRanking<ScoreRecord> { Document = doc, Ranking = rank });
                    }
                    lastScore = GetValue(doc, leaderboardQuery.ScorePath);
                    rank++;
                }

                leaderboardResult.Results = results;

                if (firstRank > 1) // There are scores before the first in the list
                {
                    var previousQuery = new LeaderboardContinuationQuery(leaderboardQuery);
                    previousQuery.Skip = 0;
                    previousQuery.Size = leaderboardQuery.Size;
                    previousQuery.IsPrevious = true;
                    previousQuery.StartId = results.First().Document.Id;
                    leaderboardResult.Previous = SerializeContinuationQuery(previousQuery);
                }

                if (documents.Count > leaderboardQuery.Size || (leaderboardQuery as LeaderboardContinuationQuery)?.IsPrevious == true) // There are scores after the last in the list.
                {
                    var nextQuery = new LeaderboardContinuationQuery(leaderboardQuery);
                    nextQuery.Skip = 0;
                    nextQuery.Size = leaderboardQuery.Size;
                    nextQuery.IsPrevious = false;
                    nextQuery.StartId = results.Last().Document.Id;

                    leaderboardResult.Next = SerializeContinuationQuery(nextQuery);
                }

                var ctx = new QueryResponseCtx(leaderboardQuery, leaderboardResult);

                await eventHandlers().RunEventHandler(eh => eh.OnQueryResponse(ctx), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running QueryResponse event handlers", ex));
            }

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

        private static Lazy<byte[]>? _key;

        private string SerializeContinuationQuery(LeaderboardContinuationQuery query)
        {
            Debug.Assert(_key != null);
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(query)));
            //return JWT.Encode(query, _key.Value, JwsAlgorithm.HS256);
        }

        private LeaderboardContinuationQuery DeserializeContinuationQuery(string continuation)
        {
            Debug.Assert(_key != null);
            return JsonConvert.DeserializeObject<LeaderboardContinuationQuery>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(continuation)));
            //return JWT.Decode<LeaderboardContinuationQuery>(continuation, _key.Value);
        }

        public Task<LeaderboardResult<ScoreRecord>> QueryCursor(string cursor, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                throw new ClientException("Invalid continuation: no more results available");
            }
            var query = DeserializeContinuationQuery(cursor);

            return Query(query, cancellationToken);
        }

        public Task UpdateScore(string id, string leaderboardName, Func<ScoreRecord?, Task<ScoreRecord>> updater)
        {
            return UpdateScores(Enumerable.Repeat(new LeaderboardEntryId(leaderboardName, id), 1), (i, old) => updater(old));
        }

        private Random random = new Random();

        private string GetDocumentId(string leaderboardName, string id)
        {
            return $"{leaderboardName}#{id}";
        }

        private LeaderboardEntryId GetEntryIdFromDocumentId(string id)
        {
            var els = id.Split('#');
            return new LeaderboardEntryId(els[0], els[1]);
        }

        public async Task UpdateScores(IEnumerable<LeaderboardEntryId> ids, Func<LeaderboardEntryId, ScoreRecord?, Task<ScoreRecord>> scoreUpdater)
        {
            var client = await CreateESClient<ScoreRecord>("");
            var results = ids.ToDictionary(id => id, id => false);
            var tries = 0;
            var success = false;
            do
            {
                var idsToUpdate = results.Where(e => !e.Value).Select(e => GetDocumentId(e.Key.LeaderboardName, e.Key.Id));
                var indices = results.Where(e => !e.Value).Select(e => GetIndex(e.Key.LeaderboardName)).Distinct();
                var indicesParams = string.Join(",", indices);
                var response = await client.MultiGetAsync(desc => desc.GetMany<ScoreRecord>(idsToUpdate, (mgdesc, _) => mgdesc.Index(indicesParams)).Index(indicesParams));

                var records = response.GetMany<ScoreRecord>(idsToUpdate).ToList();

                var updates = new List<(ScoreUpdate, Nest.IMultiGetHit<ScoreRecord>)>();
                foreach (var record in records)
                {
                    var currentScore = record.Source;
                    var id = GetEntryIdFromDocumentId(record.Id);

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
                        updates.Add((new ScoreUpdate { OldValue = record.Source, NewValue = score! }, record));
                    }
                    else//No need to update
                    {
                        results[id] = true;
                    }
                }

                var ctx = new UpdatingScoreCtx(updates.Select(u => u.Item1));
                await eventHandlers().RunEventHandler(e => e.UpdatingScores(ctx), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running leaderboard.UpdatingScore event handler", ex));

                var bulkResponse = await client.BulkAsync(desc =>
                {
                    foreach (var (score, record) in updates)
                    {
                        if (score.NewValue != null && score.OldValue != null)
                        {
                            desc = desc.Index<ScoreRecord>(s =>
                            {
                                s = s.Id(record.Id).Document(score.NewValue);
                                s = s.IfPrimaryTerm(record.PrimaryTerm);
                                s = s.IfSequenceNumber(record.SequenceNumber);
                                s = s.Index(record.Index);
                                return s;
                            });
                        }
                        else if (score.NewValue == null && score.OldValue != null)
                        {
                            desc = desc.Delete<ScoreRecord>(s => s.Id(record.Id).IfPrimaryTerm(record.PrimaryTerm).IfSequenceNumber(record.SequenceNumber).Index(record.Index));
                        }
                        else if (score.NewValue != null && score.OldValue == null)
                        {
                            var index = GetIndex(score.NewValue.LeaderboardName);

                            desc = desc.Create<ScoreRecord>(s => s
                                .Id(GetDocumentId(score.NewValue.LeaderboardName, score.NewValue.Id))
                                .Document(score.NewValue)
                                .Index(index));
                        }
                    }
                    return desc;
                });

                foreach (var item in bulkResponse.Items)
                {
                    if (item.IsValid)
                    {
                        results[GetEntryIdFromDocumentId(item.Id)] = true;
                    }
                }
                success = results.Select(kvp => kvp.Value).All(v => v);

                if (!success)
                {
                    tries++;
                    //Wait for a random duration before retry to minimize risk of further conflicts.
                    await Task.Delay(random.Next(100, 500));
                }
            }
            while (!success && tries < 5);
        }

        //public async Task IncrementScore(int increment, string userId, string leaderboardName, LeaderboardOptions options = null)
        //{
        //    if (string.IsNullOrWhiteSpace(userId))
        //    {
        //        throw new ArgumentException("Score's Id field is mandatory.", nameof(userId));
        //    }
        //    var index = options?.Index ?? leaderboardName;
        //    var client = await CreateESClient<ScoreRecord>(index);
        //    var a = new ScoreRecord();
        //    a.Score += increment;
        //    a.CreatedOn = DateTime.UtcNow;
        //    await client.UpdateAsync<ScoreRecord>(userId, rq => rq.Script(sd => sd.Source($"ctx._source.score+={increment}; ctx._source.createdOn='{DateTime.UtcNow.ToString("o")}'")).RetryOnConflict(10));
        //}

        public async Task RemoveLeaderboardEntry(string leaderboardName, string entryId)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            await client.DeleteAsync<ScoreRecord>(entryId);
        }

        public async Task ClearAllScores()
        {
            await eventHandlers().RunEventHandler(eh => eh.ClearAllScores(), ex => _logger.Log(LogLevel.Error, "leaderboard", "An error occured while running leaderboards clear all scores event handlers", ex));

            var client = await CreateESClient<ScoreRecord>("*");

            var index = client.ConnectionSettings.DefaultIndex;
            await client.Indices.DeleteAsync(index);
        }

        public async Task ClearAllScores(string leaderboardName)
        {
            await eventHandlers().RunEventHandler(eh => eh.ClearAllScores(leaderboardName), ex => _logger.Log(LogLevel.Error, "leaderboard", $"An error occured while running leaderboards clear all scores event handlers for leaderboard {leaderboardName}", ex));

            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            await client.Indices.DeleteAsync(client.ConnectionSettings.DefaultIndex);
        }

        public async Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards()
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            return (await client.SearchAsync<QuickAccessLeaderboard>(s =>
               s.Sort(so => so.Ascending("leaderboardName.keyword"))
                .From(0).Size(100))).Documents.ToList();
        }

        public async Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard)
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            await client.IndexDocumentAsync(leaderboard);
        }

        public async Task RemoveQuickAccessLeaderboard(string leaderboardName)
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            await client.DeleteAsync<QuickAccessLeaderboard>(leaderboardName);
        }

        private Nest.QueryContainer CreateQuery(
            Nest.QueryContainerDescriptor<ScoreRecord> desc,
            LeaderboardQuery rq,
            Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>? additionalContraints = null)
        {
            var fullScorePath = "scores." + rq.ScorePath;

            return desc.Bool(s2 =>
            {
                IEnumerable<Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>> mustClauses = new List<Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>> {
                    q=>q.Term(qt=>qt.Field("leaderboardName.keyword").Value(rq.Name))
                };

                if (rq.FilteredUserIds != null && rq.FilteredUserIds.Any())
                {
                    mustClauses = mustClauses.Concat(new Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>[] {
                        q => q.Ids(s=>s.Values(rq.FilteredUserIds.Select(i=>GetDocumentId(rq.Name,i.ToString()))))
                    });
                }

                if (rq.FieldFilters != null && rq.FieldFilters.Any())
                {
                    mustClauses = mustClauses.Concat(rq.FieldFilters.Select<FieldFilter, Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>>(f =>
                    {
                        if (f.Value.Type == JTokenType.Object)
                        {
                            throw new ArgumentException("Cannot use a JSON object as value for a Terms query", f.Value.ToString());
                        }
                        if (f.Value.Type == JTokenType.Array)
                        {
                            if (f.Value.Any(element => element.Type == JTokenType.Array || element.Type == JTokenType.Object))
                            {
                                throw new ArgumentException("Cannot use an array nor an object for a terms query", $"FieldFilters[{f.Field}].Value({f.Value.ToString()})");
                            }

                            return q => q.Terms(s => s.Field("document." + f.Field).Terms(f.Value.Select(t => t.ToObject<object>())));
                        }
                        return q => q.Terms(s => s.Field("document." + f.Field).Terms(f.Value.ToObject<object>()));
                    }));
                }

                if (rq.ScoreFilters != null && rq.ScoreFilters.Any())
                {
                    mustClauses = mustClauses.Concat(rq.ScoreFilters.Select<ScoreFilter, Func<Nest.QueryContainerDescriptor<ScoreRecord>, Nest.QueryContainer>>(f =>
                    {
                        if (string.IsNullOrEmpty(f.Path))
                        {
                            throw new ArgumentException("Range filtering clause provided without a 'Path' parameter.");
                        }
                        return q => q.Range(r =>
                        {
                            r = r.Field("scores." + f.Path);
                            switch (f.Type)
                            {
                                case ScoreFilterType.GreaterThan:
                                    r = r.GreaterThan(f.Value);
                                    break;
                                case ScoreFilterType.GreaterThanOrEqual:
                                    r = r.GreaterThanOrEquals(f.Value);
                                    break;
                                case ScoreFilterType.LesserThan:
                                    r = r.LessThan(f.Value);
                                    break;
                                case ScoreFilterType.LesserThanOrEqual:
                                    r = r.LessThanOrEquals(f.Value);
                                    break;
                                default:
                                    break;
                            }

                            return r;
                        });
                    }));
                }
                if (additionalContraints != null)
                {
                    mustClauses = mustClauses.Concat(new[] { additionalContraints });
                }
                return s2.Must(mustClauses);
            });
        }
    }
}
