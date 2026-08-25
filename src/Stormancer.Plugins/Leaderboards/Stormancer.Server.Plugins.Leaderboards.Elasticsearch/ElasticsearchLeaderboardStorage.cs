using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Stormancer.Server.Plugins.Leaderboards.Elasticsearch.ILeaderboarIndexMapping;

namespace Stormancer.Server.Plugins.Leaderboards.Elasticsearch
{
    internal class ElasticsearchLeaderboardStorage : ILeaderboardStorage
    {

        private readonly IESClientFactory _clientFactory;
        private Func<IEnumerable<ILeaderboardIndexMapping>> _leaderboardIndexMapping;

        public ElasticsearchLeaderboardStorage(
            IESClientFactory clientFactory,
            Func<IEnumerable<ILeaderboardIndexMapping>> leaderboardIndexMapping
            )
        {
            _clientFactory = clientFactory;
            _leaderboardIndexMapping = leaderboardIndexMapping;
        }

        private async Task<Nest.IElasticClient> CreateESClient<T>(params object[] parameters)
        {
            var result = await _clientFactory.CreateClient<T>(LeaderboardConstants.INDEX_ID, parameters);
            return result;
        }

        private string GetDocumentId(string leaderboardName, string id)
        {
            return $"{leaderboardName}#{id}";
        }

        private string GetModifiedLeaderboardName(string leaderboardName)
        {
            foreach (var mapper in _leaderboardIndexMapping())
            {
                var mapping = mapper.GetIndex(leaderboardName);
                if (!string.IsNullOrEmpty(mapping))
                {
                    return mapping;
                }
            }
            return leaderboardName;
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


        public async Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard)
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            await client.IndexDocumentAsync(leaderboard);
        }

        public async Task ClearAllScores(string leaderboardName)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);

            var client = await CreateESClient<ScoreRecord>(index);
            await client.Indices.DeleteAsync(client.ConnectionSettings.DefaultIndex);
        }

        public async Task ClearAllScores()
        {
            var client = await CreateESClient<ScoreRecord>("*");

            var index = client.ConnectionSettings.DefaultIndex;
            await client.Indices.DeleteAsync(index);
        }

        public async Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards()
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            return (await client.SearchAsync<QuickAccessLeaderboard>(s =>
               s.Sort(so => so.Ascending("leaderboardName.keyword"))
                .From(0).Size(100))).Documents.ToList();
        }

        public async Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, bool enableExaequo, CancellationToken cancellationToken)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);

            var scoreValue = score.GetValue(filters.ScorePath);
            var fullScorePath = "scores." + filters.ScorePath;
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

                                if (!enableExaequo)
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

                                if (!enableExaequo)
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

        public async Task<ScoreRecord?> GetScore(string leaderboardName, string playerId)
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

        public async Task<Dictionary<string, ScoreRecord?>> GetScores(string leaderboardName, IEnumerable<string> playerIds)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);

            var finalResults = playerIds.ToDictionary(id => id, _ => default(ScoreRecord));

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

        public async Task<long> GetTotal(string leaderboardName, LeaderboardQuery filters, CancellationToken cancellationToken)
        {
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

        public Nest.QueryContainer CreatePreviousPaginationFilter(Nest.QueryContainerDescriptor<ScoreRecord> q, ScoreRecord pivot, string path, LeaderboardOrdering leaderboardOrdering)
        {
            var pivotScore = pivot.GetValue(path);
            var fullScorePath = "scores." + path;
            // descending : ( score > pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id < pivot.Id) 
            // ascending :  ( score < pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id < pivot.Id) 
            return q.Bool(
                b1 => b1.Should(
                    q1 => q1.Range(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(fullScorePath).GreaterThan(pivotScore) : r.Field(fullScorePath).LessThan(pivotScore)),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThan(pivot.CreatedOn))
                        )
                    ),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThanOrEquals(pivot.CreatedOn).GreaterThanOrEquals(pivot.CreatedOn)),
                            q2 => q2.TermRange(r => r.Field(record => record.Id).LessThan(pivot.Id))
                        )
                    )
                )
            );
        }

        public Nest.QueryContainer CreateNextPaginationFilter(Nest.QueryContainerDescriptor<ScoreRecord> q, ScoreRecord pivot, string path, LeaderboardOrdering leaderboardOrdering)
        {
            var pivotScore = pivot.GetValue(path);
            var fullScorePath = "scores." + path;

            // descending : ( score < pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.createdOn AND Id > pivot.Id) 
            // ascending :  ( score > pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.createdOn AND Id > pivot.Id) 
            return q.Bool(
                b1 => b1.Should(
                    q1 => q1.Range(r => leaderboardOrdering == LeaderboardOrdering.Descending ? r.Field(fullScorePath).LessThan(pivotScore) : r.Field(fullScorePath).GreaterThan(pivotScore)),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).GreaterThan(pivot.CreatedOn))
                        )
                    ),
                    q1 => q1.Bool(
                        b2 => b2.Must(
                            q2 => q2.Term(t => t.Field(fullScorePath).Value(pivotScore)),
                            q2 => q2.DateRange(r => r.Field(record => record.CreatedOn).LessThanOrEquals(pivot.CreatedOn).GreaterThanOrEquals(pivot.CreatedOn)),
                            q2 => q2.TermRange(r => r.Field(record => record.Id).GreaterThan(pivot.Id))
                        )
                    )
                )
            );
        }

        public async Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery leaderboardQuery, bool enableExaequo, CancellationToken cancellationToken)
        {
            var index = GetModifiedLeaderboardName(leaderboardQuery.Name);

            var fullScorePath = "scores." + leaderboardQuery.ScorePath;
            var leaderboardContinuationQuery = leaderboardQuery as LeaderboardContinuationQuery;

            var isContinuation = leaderboardContinuationQuery != null;
            var isPreviousContinuation = leaderboardContinuationQuery != null && leaderboardContinuationQuery.IsPrevious;

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
            leaderboardResult.Total = await GetTotal(leaderboardQuery.Name, leaderboardQuery, cancellationToken);

            // Compute rankings
            if (documents.Any())
            {
                int firstRank = 0;
                try
                {
                    firstRank = (int)await GetRanking(documents.First(), leaderboardQuery, leaderboardQuery.Name, enableExaequo, cancellationToken);
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


                    if (enableExaequo)
                    {
                        int currentRank;
                        var v = doc.GetValue(leaderboardQuery.ScorePath);
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
                    lastScore = doc.GetValue(leaderboardQuery.ScorePath);
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
                    leaderboardResult.Previous = previousQuery.SerializeContinuationQuery();
                }

                if (documents.Count > leaderboardQuery.Size || (leaderboardQuery as LeaderboardContinuationQuery)?.IsPrevious == true) // There are scores after the last in the list.
                {
                    var nextQuery = new LeaderboardContinuationQuery(leaderboardQuery);
                    nextQuery.Skip = 0;
                    nextQuery.Size = leaderboardQuery.Size;
                    nextQuery.IsPrevious = false;
                    nextQuery.StartId = results.Last().Document.Id;

                    leaderboardResult.Next = nextQuery.SerializeContinuationQuery();
                }               
            }

            return leaderboardResult;
        }

        public async Task RemoveEntry(string leaderboardName, string entryId)
        {
            var index = GetModifiedLeaderboardName(leaderboardName);
            var client = await CreateESClient<ScoreRecord>(index);
            await client.DeleteAsync<ScoreRecord>(entryId);
        }

        public async Task RemoveQuickAccessLeaderboard(string leaderboardName)
        {
            var client = await CreateESClient<QuickAccessLeaderboard>("QuickAccessLeaderboads");
            await client.DeleteAsync<QuickAccessLeaderboard>(leaderboardName);
        }

        public string GetIndex(string leaderboardName)
        {
            return _clientFactory.GetIndex<ScoreRecord>(LeaderboardConstants.INDEX_ID, GetModifiedLeaderboardName(leaderboardName));
        }

        private LeaderboardEntryId GetEntryIdFromDocumentId(string id)
        {
            var els = id.Split('#');
            return new LeaderboardEntryId(els[0], els[1]);
        }

        public async Task UpdateScores(Dictionary<LeaderboardEntryId, bool> results, Func<IReadOnlyDictionary<LeaderboardEntryId, ScoreRecord>, Task<Dictionary<LeaderboardEntryId, ScoreUpdate>>> updateRecords)
        {
            var client = await CreateESClient<ScoreRecord>("");

            var tries = 0;
            var success = false;
            do
            {
                var idsToUpdate = results.Where(e => !e.Value).Select(e => GetDocumentId(e.Key.LeaderboardName, e.Key.Id));
                var indices = results.Where(e => !e.Value).Select(e => GetIndex(e.Key.LeaderboardName)).Distinct();
                var indicesParams = string.Join(",", indices);
                var response = await client.MultiGetAsync(desc => desc.GetMany<ScoreRecord>(idsToUpdate, (mgdesc, _) => mgdesc.Index(indicesParams)).Index(indicesParams));

                var hits = response.GetMany<ScoreRecord>(idsToUpdate).ToList();
                var hitsById = hits.ToDictionary(r => GetEntryIdFromDocumentId(r.Id));
                var recordsById = hits.ToDictionary(r => GetEntryIdFromDocumentId(r.Id), r => r.Source);
                var updates = await updateRecords(recordsById);

                var bulkResponse = await client.BulkAsync(desc =>
                {
                    foreach (var (id, score) in updates)
                    {
                        if (score.NewValue != null && score.OldValue != null)
                        {
                            desc = desc.Index<ScoreRecord>(s =>
                            {
                                s = s.Id(hitsById[id].Id).Document(score.NewValue);
                                s = s.IfPrimaryTerm(hitsById[id].PrimaryTerm);
                                s = s.IfSequenceNumber(hitsById[id].SequenceNumber);
                                s = s.Index(hitsById[id].Index);
                                return s;
                            });
                        }
                        else if (score.NewValue == null && score.OldValue != null)
                        {
                            desc = desc.Delete<ScoreRecord>(s => s.Id(hitsById[id].Id).IfPrimaryTerm(hitsById[id].PrimaryTerm).IfSequenceNumber(hitsById[id].SequenceNumber).Index(hitsById[id].Index));
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
                    await Task.Delay(Random.Shared.Next(100, 500));
                }
            }
            while (!success && tries < 5);
        }
    }
}
