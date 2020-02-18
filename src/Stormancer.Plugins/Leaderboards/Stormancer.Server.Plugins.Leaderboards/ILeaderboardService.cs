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
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    public enum UpdateScorePolicy
    {
        UpdateAlways,
        UpdateIfGreater,
        UpdateIfLower
    }

    public struct LeaderboardEntryId
    {
        public LeaderboardEntryId(string leaderboard, string id)
        {
            LeaderboardName = leaderboard;
            Id = id;
        }

        public string LeaderboardName { get; set; }

        public string Id { get; set; }

        public override int GetHashCode()
        {
            return LeaderboardName.GetHashCode() * 17 + Id.GetHashCode();
        }

        public override bool Equals(object obj)
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
        /// Runs a leaderboard query
        /// </summary>
        /// <param name="query">An object representing the leaderboard query.</param>
        /// <returns>A set of leaderboard results, as well as 2 leaderboard cursors to get the previous and next pages if they exist.</returns>
        Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery query);

        /// <summary>
        /// Gets the set of leaderboard results associated with the provided cursor.
        /// </summary>
        /// <param name="cursor">A leaderboard query cursor. A cursor is associated to a leaderboard page and can be fetched from a leaderboard result.</param>
        /// <returns>A set of leaderboard results, as well as 2 leaderboard cursors to get the previous and next pages if they exist.</returns>
        Task<LeaderboardResult<ScoreRecord>> QueryCursor(string cursor);

        /// <summary>
        /// Updates a score in the leaderboard
        /// </summary>
        /// <param name="scoreUpdater">An updater function</param>
        /// <param name="leaderboardName">Name of the leaderboard</param>
        /// <param name="options">An pptional parameter representing leaderboard options</param>
        /// <remarks>
        ///     <list>
        ///         If scoreUpdater param is null, returned score is created in the database.
        ///         If scoreUpdater returns null, score is deleted from the database.
        ///         If scoreUpdate param is not null, and scoreUpdate returns a value. The score is updated with the value in the database.
        ///     </list>
        /// </remarks>
        /// <returns></returns>
        Task UpdateScore(string id, string leaderboardName, Func<ScoreRecord, Task<ScoreRecord>> scoreUpdater);

        Task UpdateScores(IEnumerable<LeaderboardEntryId> ids, Func<LeaderboardEntryId, ScoreRecord, Task<ScoreRecord>> scoreUpdater); 
        
        /// <summary>
        /// </summary>
        /// <param name="leaderboardName"></param>
        /// <param name="entryId"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        Task RemoveLeaderboardEntry(string leaderboardName, string entryId);

        Task ClearAllScores();

        Task ClearAllScores(string leaderboardName);

        Task<ScoreRecord> GetScore(string playerId, string leaderboardName);

        Task<Dictionary<string, ScoreRecord>> GetScores(List<string> playerIds, string leaderboardName);

        Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName);

        Task<long> GetTotal(LeaderboardQuery filters, string leaderboardName);

        Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards();

        Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard);

        Task RemoveQuickAccessLeaderboard(string leaderboardName);
    }
}
