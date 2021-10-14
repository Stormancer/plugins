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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{

    /// <summary>
    /// Id of a leaderboard entry.
    /// </summary>
    public struct LeaderboardEntryId
    {
        /// <summary>
        /// Creates a <see cref="LeaderboardEntryId"/> instance.
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="id"></param>
        public LeaderboardEntryId(string leaderboard, string id)
        {
            LeaderboardName = leaderboard;
            Id = id;
        }

        /// <summary>
        /// Name of the leaderboard.
        /// </summary>
        public string LeaderboardName { get; set; }

        /// <summary>
        /// Id of the score in the leaderboard.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Computes an hashcode.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return LeaderboardName.GetHashCode() * 17 + Id.GetHashCode();
        }

        /// <summary>
        /// Equals
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            if(!(obj is LeaderboardEntryId other))
            {
                return false;
            }

            return other.Id.Equals(this.Id) && other.LeaderboardName.Equals(this.LeaderboardName);
        }
    }

    /// <summary>
    /// Provides methods to query and manage leaderboards.
    /// </summary>
    public interface ILeaderboardService
    {
        /// <summary>
        /// Gets the index of a leaderboard.
        /// </summary>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        string GetIndex(string leaderboardName);

        /// <summary>
        /// Runs a leaderboard query
        /// </summary>
        /// <param name="query">An object representing the leaderboard query.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A set of leaderboard results, as well as 2 leaderboard cursors to get the previous and next pages if they exist.</returns>
        Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery query,CancellationToken cancellationToken);

        /// <summary>
        /// Gets the set of leaderboard results associated with the provided cursor.
        /// </summary>
        /// <param name="cursor">A leaderboard query cursor. A cursor is associated to a leaderboard page and can be fetched from a leaderboard result.</param>
        /// <returns>A set of leaderboard results, as well as 2 leaderboard cursors to get the previous and next pages if they exist.</returns>
        Task<LeaderboardResult<ScoreRecord>> QueryCursor(string cursor, CancellationToken cancellationToken);

        /// <summary>
        /// Updates a score in the leaderboard
        /// </summary>
        /// <param name="id">id of the score to look for.</param>
        /// <param name="scoreUpdater">An updater function</param>
        /// <param name="leaderboardName">Name of the leaderboard</param>
        /// <remarks>
        ///     <list>
        ///         If scoreUpdater param is null, returned score is created in the database.
        ///         If scoreUpdater returns null, score is deleted from the database.
        ///         If scoreUpdate param is not null, and scoreUpdate returns a value. The score is updated with the value in the database.
        ///     </list>
        /// </remarks>
        /// <returns></returns>
        Task UpdateScore(string id, string leaderboardName, Func<ScoreRecord?, Task<ScoreRecord>> scoreUpdater);

        /// <summary>
        /// Batch update of several scores in leaderboards.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="scoreUpdater"></param>
        /// <returns></returns>
        Task UpdateScores(IEnumerable<LeaderboardEntryId> ids, Func<LeaderboardEntryId, ScoreRecord?, Task<ScoreRecord>> scoreUpdater); 
        
        /// <summary>
        /// Removes an entry in a leaderboard.
        /// </summary>
        /// <param name="leaderboardName"></param>
        /// <param name="entryId"></param>
        /// <returns></returns>
        Task RemoveLeaderboardEntry(string leaderboardName, string entryId);

        /// <summary>
        /// Clears all scores in all leaderboards.
        /// </summary>
        /// <remarks>This operation is definitive. Use with caution.</remarks>
        /// <returns></returns>
        Task ClearAllScores();

        /// <summary>
        /// Clears all scores in a leaderboard.
        /// </summary>
        /// <remarks>
        /// * is supported in leaderboard names to delete several leaderboards at once.
        /// </remarks>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task ClearAllScores(string leaderboardName);

        /// <summary>
        /// Gets a score entry in a leaderboard
        /// </summary>
        /// <param name="id"></param>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task<ScoreRecord?> GetScore(string id, string leaderboardName);

        /// <summary>
        /// Gets scores in a leaderboard
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task<Dictionary<string, ScoreRecord?>> GetScores(List<string> ids, string leaderboardName);

        /// <summary>
        /// Gets the ranking associated with a score in a leaderboard.
        /// </summary>
        /// <param name="score"></param>
        /// <param name="filters"></param>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the total of scores matching a filter in a leaderboard.
        /// </summary>
        /// <param name="filters"></param>
        /// <param name="leaderboardName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<long> GetTotal(LeaderboardQuery filters, string leaderboardName, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a list of favorite leaderboards.
        /// </summary>
        /// <returns></returns>
        Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards();

        /// <summary>
        /// Adds a leaderboard to the quick access list.
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <returns></returns>
        Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard);


        /// <summary>
        /// Removes a leaderboard from the quick access list.
        /// </summary>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task RemoveQuickAccessLeaderboard(string leaderboardName);
    }
}
