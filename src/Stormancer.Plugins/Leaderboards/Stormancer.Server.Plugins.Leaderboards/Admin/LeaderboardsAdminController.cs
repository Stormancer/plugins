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

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    /// <summary>
    /// Provides Admin web APIs for controllers.
    /// </summary>
    [ApiController]
    [Route("_leaderboards")]
    public class LeaderboardsAdminController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboards;

        /// <summary>
        /// Creates a new <see cref="LeaderboardsAdminController"/>.
        /// </summary>
        /// <param name="leaderboards"></param>
        public LeaderboardsAdminController(ILeaderboardService leaderboards)
        {
            _leaderboards = leaderboards;
        }


        /// <summary>
        /// Queries a leaderboard.
        /// </summary>
        /// <param name="boardName"></param>
        /// <param name="cursor"></param>
        /// <param name="count"></param>
        /// <param name="ordering"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet("{boardName}")]
        public async Task<LeaderboardResult<ScoreRecord>> Get(string boardName, string? cursor = null, int count = 50, string ordering = "desc", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                var leaderboardOrdering = (ordering == "desc" ? LeaderboardOrdering.Descending : LeaderboardOrdering.Ascending);
                var result = await _leaderboards.Query(new LeaderboardQuery { Name = boardName, Size = count, Order = leaderboardOrdering },cancellationToken);
                var total = await _leaderboards.GetTotal(new LeaderboardQuery { Name = boardName }, boardName,cancellationToken);
                result.Total = total;
                return result;
            }
            else
            {
                var result = await _leaderboards.QueryCursor(cursor,cancellationToken);
                return result;
            }
        }

        /// <summary>
        /// Updates an entry in a leaderboard.
        /// </summary>
        /// <param name="boardName"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        [HttpPost("{boardName}")]
        public Task Post(string boardName, [FromBody]ScoreRecord record)
        {
            return _leaderboards.UpdateScore(record.Id, boardName, old => Task.FromResult(record));
        }

        /// <summary>
        /// Deletes all leaderboards
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        public async Task Delete()
        {
            await _leaderboards.ClearAllScores();
        }

        /// <summary>
        /// Deletes a leaderboard.
        /// </summary>
        /// <param name="boardName"></param>
        /// <returns></returns>
        [HttpDelete("{boardName}")]
        public async Task Delete(string boardName)
        {
            await _leaderboards.ClearAllScores(boardName);
        }

        /// <summary>
        /// Deletates an entry in a leaderboard.
        /// </summary>
        /// <param name="boardName"></param>
        /// <param name="entryId"></param>
        /// <returns></returns>
        [HttpDelete("{boardName}/{entryId}")]
        public async Task Delete(string boardName, string entryId)
        {
            await _leaderboards.RemoveLeaderboardEntry(boardName, entryId);
        }
    }

    /// <summary>
    /// Provides web APIs to manage leaderboard quick links.
    /// </summary>
    [ApiController]
    [Route("_leaderboardsquickaccess")]
    public class LeaderboardsQuickAccessAdminController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboards;

        /// <summary>
        /// Creates a <see cref="LeaderboardsQuickAccessAdminController"/>.
        /// </summary>
        /// <param name="leaderboards"></param>
        public LeaderboardsQuickAccessAdminController(ILeaderboardService leaderboards)
        {
            _leaderboards = leaderboards;
        }

        /// <summary>
        /// Gets the leaderboard quick links.
        /// </summary>
        /// <returns></returns>
        [HttpGet("quickaccess")]
        public Task<List<QuickAccessLeaderboard>> GetQuickAccess()
        {
            return _leaderboards.GetQuickAccessLeaderboards();
        }

        /// <summary>
        /// Adds a leaderboard quick link.
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <returns></returns>
        [HttpPost("quickaccess")]
        public Task PostQuickAccess([FromBody]QuickAccessLeaderboard leaderboard)
        {
            return _leaderboards.AddQuickAccessLeaderboard(leaderboard);
        }

        /// <summary>
        /// Removes a leaderboard quick link.
        /// </summary>
        /// <param name="boardName"></param>
        /// <returns></returns>
        [HttpDelete("quickaccess/{boardName}")]
        public Task DeleteQuickAccess(string boardName)
        {
            return _leaderboards.RemoveQuickAccessLeaderboard(boardName);
        }
    }
}
