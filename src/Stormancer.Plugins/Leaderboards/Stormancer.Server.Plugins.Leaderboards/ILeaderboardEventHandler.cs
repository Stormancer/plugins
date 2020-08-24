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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    /// <summary>
    /// classes implementing the contract can alter the name of the leaderboard used to generate the index name.
    /// </summary>
    /// <remarks>
    /// Leaderboards can be stored in individual indices or merged into a single bigger index. 2 leaderboards (different leaderboardName) can be stored in the same index
    /// if <see cref="GetIndex(string)">GetIndex</see> returns the same value for both of them.
    /// </remarks>
    public interface ILeaderboardIndexMapping
    {
        /// <summary>
        /// Gets the leaderboard id that should be used for index lookup.
        /// </summary>
        /// <param name="leaderboardName">A leaderboard name</param>
        /// <returns>The id to use to locate the leaderboard index. If null, the leaderboard name will be used.</returns>
        string? GetIndex(string leaderboardName);
    }

    /// <summary>
    /// Classes implementing the contract can participate in the leaderboard system event flow.
    /// </summary>
    public interface ILeaderboardEventHandler
    {
        /// <summary>
        /// Called before scores are being persisted in the DB.
        /// </summary>
        /// <param name="score"></param>
        /// <returns></returns>
        Task UpdatingScores(UpdatingScoreCtx score) => Task.CompletedTask;

        /// <summary>
        /// Called after scores where retrieved and before they are returned to the caller.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        Task OnQueryResponse(QueryResponseCtx response) => Task.CompletedTask;

        /// <summary>
        /// Called when the leaderboard  system receives a query.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        Task OnQueryingLeaderboard(LeaderboardQuery query) => Task.CompletedTask;

        /// <summary>
        /// Called before scores of a leaderboard are cleared.
        /// </summary>
        /// <param name="leaderboardName"></param>
        /// <returns></returns>
        Task ClearAllScores(string leaderboardName) => Task.CompletedTask;

        /// <summary>
        /// Called before all scores from all leaderboards are cleared.
        /// </summary>
        /// <returns></returns>
        Task ClearAllScores() => Task.CompletedTask;
    }

    /// <summary>
    /// Context passed to the <see cref="ILeaderboardEventHandler.UpdatingScores(UpdatingScoreCtx)"/> event.
    /// </summary>
    public class UpdatingScoreCtx
    {
        internal UpdatingScoreCtx( IEnumerable<ScoreUpdate> updates)
        {
            Updates = updates;
        }

        /// <summary>
        /// Score updates.
        /// </summary>
       public IEnumerable<ScoreUpdate> Updates { get; }
    }

    /// <summary>
    /// Represents a score update.
    /// </summary>
    public class ScoreUpdate
    {
        /// <summary>
        /// Old value.
        /// </summary>
        /// <remarks>
        /// New score if null.
        /// </remarks>
        public ScoreRecord? OldValue { get; set; }

        /// <summary>
        /// New value.
        /// </summary>
        /// <remarks>
        /// Delete score if null.
        /// </remarks>
        public ScoreRecord? NewValue { get; set; }
    }

    /// <summary>
    /// Represents a query response passed to <see cref="ILeaderboardEventHandler.OnQueryResponse(QueryResponseCtx)"/>
    /// </summary>
    public class QueryResponseCtx
    {
        internal QueryResponseCtx(LeaderboardQuery query, LeaderboardResult<ScoreRecord> result)
        {
            Query = query;
            Result = result;
        }

        /// <summary>
        /// The query.
        /// </summary>
        public LeaderboardQuery Query { get; }

        /// <summary>
        /// The result.
        /// </summary>
        public LeaderboardResult<ScoreRecord> Result { get; set; }
    }
}
