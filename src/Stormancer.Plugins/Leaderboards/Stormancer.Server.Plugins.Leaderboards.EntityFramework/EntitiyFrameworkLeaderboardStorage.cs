
using Microsoft.EntityFrameworkCore;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Leaderboards.EntityFramework.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework
{
    internal class EntitiyFrameworkLeaderboardStorage : ILeaderboardStorage
    {
        private readonly DbContextAccessor _dbContext;
        private readonly ILogger _logger;

        public EntitiyFrameworkLeaderboardStorage(DbContextAccessor dbContext, ILogger logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard)
        {
            var record = QuickAccessLeaderboardEntity.CreateEntityFromLeaderboard(leaderboard);
            var dbContext = await _dbContext.GetDbContextAsync();

            await dbContext.Set<QuickAccessLeaderboardEntity>().AddAsync(record);
            await dbContext.SaveChangesAsync();
        }

        private IQueryable<ScoreEntity> ApplyFilters(IQueryable<ScoreEntity> query, LeaderboardQuery filters)
        {
            query = query.Where(s => s.LeaderboardName == filters.Name);

            if (filters.FilteredUserIds != null && filters.FilteredUserIds.Any())
            {
                query = query.Where(s => filters.FilteredUserIds.Contains(s.Id));
            }

            if (filters.FieldFilters != null && filters.FieldFilters.Any())
            {
                foreach (var fieldFilter in filters.FieldFilters)
                {
                    switch (fieldFilter.Value.Type)
                    {
                        case Newtonsoft.Json.Linq.JTokenType.String:
                            var filterString = fieldFilter.Value.ToString();
                            query = query.Where(s => s.Document.RootElement.GetProperty(fieldFilter.Field).GetString() == filterString);
                            break;
                        case Newtonsoft.Json.Linq.JTokenType.Integer:
                            var filterInt = fieldFilter.Value.ToObject<int>();
                            query = query.Where(s => s.Document.RootElement.GetProperty(fieldFilter.Field).GetInt32() == filterInt);
                            break;
                        case Newtonsoft.Json.Linq.JTokenType.Float:
                            var filterDouble = fieldFilter.Value.ToObject<double>();
                            query = query.Where(s => s.Document.RootElement.GetProperty(fieldFilter.Field).GetDouble() == filterDouble);
                            break;
                        case Newtonsoft.Json.Linq.JTokenType.Boolean:
                            var filterBool = fieldFilter.Value.ToObject<bool>();
                            query = query.Where(s => s.Document.RootElement.GetProperty(fieldFilter.Field).GetBoolean() == filterBool);
                            break;
                        case Newtonsoft.Json.Linq.JTokenType.Date:
                            var filterdDate = fieldFilter.Value.ToObject<DateTime>();
                            query = query.Where(s => s.Document.RootElement.GetProperty(fieldFilter.Field).GetDateTime() == filterdDate);
                            break;
                        default:
                            throw new NotSupportedException($"Field filter of type {fieldFilter.Value.Type} is not supported.");
                    }
                }
            }

            if (filters.ScoreFilters != null && filters.ScoreFilters.Any())
            {
                foreach (var scoreFilter in filters.ScoreFilters)
                {
                    switch (scoreFilter.Type)
                    {
                        case ScoreFilterType.GreaterThanOrEqual:
                            query = query.Where(s => s.Scores.RootElement.GetProperty(scoreFilter.Path).GetInt64() >= scoreFilter.Value);
                            break;
                        case ScoreFilterType.GreaterThan:
                            query = query.Where(s => s.Scores.RootElement.GetProperty(scoreFilter.Path).GetInt64() > scoreFilter.Value);
                            break;
                        case ScoreFilterType.LesserThanOrEqual:
                            query = query.Where(s => s.Scores.RootElement.GetProperty(scoreFilter.Path).GetInt64() <= scoreFilter.Value);
                            break;
                        case ScoreFilterType.LesserThan:
                            query = query.Where(s => s.Scores.RootElement.GetProperty(scoreFilter.Path).GetInt64() < scoreFilter.Value);
                            break;
                    }
                }
            }

            return query;
        }

        public async Task ClearAllScores(string leaderboardName)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            await dbContext.Set<ScoreEntity>().Where(s => s.LeaderboardName == leaderboardName).ExecuteDeleteAsync();
        }

        public async Task ClearAllScores()
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            await dbContext.Set<ScoreEntity>().ExecuteDeleteAsync();
        }

        public string GetIndex(string leaderboardName)
        {
            return leaderboardName;
        }

        public async Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards()
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            return (await dbContext.Set<QuickAccessLeaderboardEntity>().ToListAsync())
                .Select(QuickAccessLeaderboardEntity.CreateLeaderboardFromEntity).ToList();
        }

        public async Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, bool enableExaequo, CancellationToken cancellationToken)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            var query = ApplyFilters(dbContext.Set<ScoreEntity>(), filters);
            var scoreValue = score.GetValue(filters.ScorePath);
            if (filters.Order == LeaderboardOrdering.Ascending)
            {
                if (enableExaequo)
                {
                    query = query.Where(s => s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() < scoreValue);
                }
                else
                {
                    query = query.Where(s => s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() < scoreValue
                    || (s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() == scoreValue
                        && s.CreatedOn < score.CreatedOn));
                }
            }
            else
            {
                if (enableExaequo)
                {
                    query = query.Where(s => s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() > scoreValue);
                }
                else
                {
                    query = query.Where(s => s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() > scoreValue
                    || (s.Scores.RootElement.GetProperty(filters.ScorePath).GetInt64() == scoreValue
                        && s.CreatedOn < score.CreatedOn));
                }
            }

            return await query.LongCountAsync(cancellationToken) + 1;
        }

        public async Task<ScoreRecord?> GetScore(string leaderboardName, string playerId)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            return ScoreEntity.CreateScoreFromEntity(await dbContext.Set<ScoreEntity>().FirstOrDefaultAsync(s => s.LeaderboardName == leaderboardName && s.Id == playerId));
        }

        public async Task<Dictionary<string, ScoreRecord?>> GetScores(string leaderboardName, IEnumerable<string> playerIds)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            var entities = await dbContext.Set<ScoreEntity>().Where(s => s.LeaderboardName == leaderboardName && playerIds.Contains(s.Id)).ToListAsync();
            var result = new Dictionary<string, ScoreRecord?>();
            foreach (var playerId in playerIds)
            {
                var entity = entities.FirstOrDefault(s => s.Id == playerId);
                result[playerId] = ScoreEntity.CreateScoreFromEntity(entity);
            }
            return result;
        }

        public async Task<long> GetTotal(string leaderboardName, LeaderboardQuery filters, CancellationToken cancellationToken)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            var query = ApplyFilters(dbContext.Set<ScoreEntity>(), filters);
            return await query.LongCountAsync(cancellationToken);
        }

        public async Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery leaderboardQuery, bool enableExaequo, CancellationToken cancellationToken)
        {
            var leaderboardContinuationQuery = leaderboardQuery as LeaderboardContinuationQuery;

            var isContinuation = leaderboardContinuationQuery != null;
            var isPreviousContinuation = leaderboardContinuationQuery != null && leaderboardContinuationQuery.IsPrevious;

            var dbContext = await _dbContext.GetDbContextAsync();
            var query = ApplyFilters(dbContext.Set<ScoreEntity>(), leaderboardQuery);

            ScoreRecord? start = null;
            if (!string.IsNullOrEmpty(leaderboardQuery.StartId))
            {
                start = await GetScore(leaderboardQuery.Name, leaderboardQuery.StartId);
                if (start == null)
                {
                    return new LeaderboardResult<ScoreRecord>() { LeaderboardName = leaderboardQuery.Name, Total = 0 };
                }
            }

            if (start != null)
            {
                var startValue = start.GetValue(leaderboardQuery.ScorePath);
                if (isPreviousContinuation)
                {
                    // descending : ( score > pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id < pivot.Id) 
                    // ascending :  ( score < pivot.score) OR (score == pivot.score AND createdOn < pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id > pivot.Id) 
                    if (leaderboardQuery.Order == LeaderboardOrdering.Descending)
                    {
                        query = query.Where(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() > startValue
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn < start.CreatedOn)
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn == start.CreatedOn && string.Compare(s.Id, start.Id) < 0));
                    }
                    else
                    {
                        query = query.Where(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() < startValue
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn < start.CreatedOn)
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn == start.CreatedOn && string.Compare(s.Id, start.Id) < 0));
                    }

                }
                else
                {
                    // descending : ( score < pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id > pivot.Id) 
                    // ascending :  ( score < pivot.score) OR (score == pivot.score AND createdOn > pivot.createdOn) OR (score == pivot.score AND createdOn == pivot.CreatedOn AND Id > pivot.Id) 
                    if (leaderboardQuery.Order == LeaderboardOrdering.Descending)
                    {
                        query = query.Where(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() < startValue
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn > start.CreatedOn)
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn == start.CreatedOn && string.Compare(s.Id, start.Id) > 0));
                    }
                    else
                    {
                        query = query.Where(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() > startValue
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn > start.CreatedOn)
                            || (s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64() == startValue && s.CreatedOn == start.CreatedOn && string.Compare(s.Id, start.Id) > 0));
                    }
                }
            }

            if (leaderboardQuery.Order == LeaderboardOrdering.Descending)
            {
                query = query.OrderByDescending(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64())
                    .ThenBy(s => s.CreatedOn)
                    .ThenBy(s => s.Id);
            }
            else
            {
                query = query.OrderBy(s => s.Scores.RootElement.GetProperty(leaderboardQuery.ScorePath).GetInt64())
                    .ThenBy(s => s.CreatedOn)
                    .ThenBy(s => s.Id);
            }

            if ((isContinuation && !isPreviousContinuation) || start == null)
            {
                query = query.Skip(leaderboardQuery.Skip).Take(leaderboardQuery.Size + 1);
            }
            else
            {
                query = query.Skip(leaderboardQuery.Skip).Take(leaderboardQuery.Size);
            }

            var scoreEntities = await query.ToListAsync(cancellationToken);

            var documents = scoreEntities.Select(ScoreEntity.CreateScoreFromEntity).ToList();
            if (!isContinuation && start != null)
            {
                documents.Insert(0, start);
            }
            else if (isPreviousContinuation)
            {
                documents.Reverse();
            }

            var leaderboardResult = new LeaderboardResult<ScoreRecord>() { LeaderboardName = leaderboardQuery.Name };
            leaderboardResult.Total = await GetTotal(leaderboardQuery.Name, leaderboardQuery, cancellationToken);

            if (documents.Any())
            {
                int firstRank = 0;
                try
                {
                    firstRank = (int)await GetRanking(documents.First()!, leaderboardQuery, leaderboardQuery.Name, enableExaequo, cancellationToken);
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
                    var previousQuery = new LeaderboardContinuationQuery(leaderboardQuery)
                    {
                        Skip = 0,
                        Size = leaderboardQuery.Size,
                        IsPrevious = true,
                        StartId = results.First().Document.Id
                    };
                    leaderboardResult.Previous = previousQuery.SerializeContinuationQuery();
                }

                if (documents.Count > leaderboardQuery.Size || (leaderboardQuery as LeaderboardContinuationQuery)?.IsPrevious == true) // There are scores after the last in the list.
                {
                    var nextQuery = new LeaderboardContinuationQuery(leaderboardQuery)
                    {
                        Skip = 0,
                        Size = leaderboardQuery.Size,
                        IsPrevious = false,
                        StartId = results.Last().Document.Id
                    };

                    leaderboardResult.Next = nextQuery.SerializeContinuationQuery();
                }
            }
            return leaderboardResult;
        }

        public async Task RemoveEntry(string leaderboardName, string entryId)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            dbContext.Set<ScoreEntity>().RemoveRange(dbContext.Set<ScoreEntity>().Where(s => s.LeaderboardName == leaderboardName && s.Id == entryId));
            await dbContext.SaveChangesAsync();
        }

        public async Task RemoveQuickAccessLeaderboard(string leaderboardName)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            dbContext.Set<QuickAccessLeaderboardEntity>().RemoveRange(dbContext.Set<QuickAccessLeaderboardEntity>().Where(s => s.LeaderboardName == leaderboardName));
            await dbContext.SaveChangesAsync();
        }

        public async Task UpdateScores(Dictionary<LeaderboardEntryId, bool> results, Func<IReadOnlyDictionary<LeaderboardEntryId, ScoreRecord>, Task<Dictionary<LeaderboardEntryId, ScoreUpdate>>> updateRecords)
        {
            var dbContext = await _dbContext.GetDbContextAsync();
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                var idsToUpdate = results.Where(kvp => !kvp.Value).ToList();
                var entitySet = dbContext.Set<ScoreEntity>();

                List<ScoreEntity> existingScores = new List<ScoreEntity>();
                    foreach(var group in idsToUpdate.GroupBy(kvp => kvp.Key.LeaderboardName))
                {
                    var leaderboardName = group.Key;
                    var ids = group.Select(kvp => kvp.Key.Id).ToList();
                    existingScores.AddRange(await entitySet.Where(s => s.LeaderboardName == leaderboardName && ids.Contains(s.Id)).ToListAsync());
                }

                var existingScoresDictionary = existingScores.ToDictionary(s => new LeaderboardEntryId { LeaderboardName = s.LeaderboardName, Id = s.Id }, ScoreEntity.CreateScoreFromEntity);
                foreach (var kvp in idsToUpdate)
                {
                    if (!existingScoresDictionary.ContainsKey(kvp.Key))
                    {
                        existingScoresDictionary[kvp.Key] = null;
                    }
                }

                var updates = await updateRecords(existingScoresDictionary);
                foreach (var (id, score) in updates)
                {
                    if(score.NewValue != null && score.OldValue != null)
                    {
                        entitySet.Update(ScoreEntity.CreateEntityFromScore(score.NewValue));                        
                    }
                    else if (score.NewValue != null && score.OldValue == null)
                    {
                        await entitySet.AddAsync(ScoreEntity.CreateEntityFromScore(score.NewValue));
                    }
                    else if (score.NewValue == null && score.OldValue != null)
                    {
                        entitySet.Remove(ScoreEntity.CreateEntityFromScore(score.OldValue));
                    }
                }
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "leaderboards.entityframeworks.updatescores", "Failed to update scores", ex);
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
