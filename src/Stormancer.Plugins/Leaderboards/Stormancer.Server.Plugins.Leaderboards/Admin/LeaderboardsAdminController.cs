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
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    [ApiController]
    [Route("_leaderboards")]
    public class LeaderboardsAdminController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboards;

        public LeaderboardsAdminController(ILeaderboardService leaderboards)
        {
            _leaderboards = leaderboards;
        }

        [HttpGet("{boardName}")]
        public async Task<LeaderboardResult<ScoreRecord>> Get(string boardName, string? cursor = null, int count = 50, string ordering = "desc")
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                var leaderboardOrdering = (ordering == "desc" ? LeaderboardOrdering.Descending : LeaderboardOrdering.Ascending);
                var result = await _leaderboards.Query(new LeaderboardQuery { Name = boardName, Count = count, Order = leaderboardOrdering });
                var total = await _leaderboards.GetTotal(new LeaderboardQuery { Name = boardName }, boardName);
                result.Total = total;
                return result;
            }
            else
            {
                var result = await _leaderboards.QueryCursor(cursor);
                return result;
            }
        }

        [HttpPost("{boardName}")]
        public Task Post(string boardName, [FromBody]ScoreRecord record)
        {
            return _leaderboards.UpdateScore(record.Id, boardName, old => Task.FromResult(record));
        }

        [HttpDelete]
        public async Task Delete()
        {
            await _leaderboards.ClearAllScores();
        }

        [HttpDelete("{boardName}")]
        public async Task Delete(string boardName)
        {
            await _leaderboards.ClearAllScores(boardName);
        }

        [HttpDelete("{boardName}/{entryId}")]
        public async Task Delete(string boardName, string entryId)
        {
            await _leaderboards.RemoveLeaderboardEntry(boardName, entryId);
        }
    }

    [ApiController]
    [Route("_leaderboardsquickaccess")]
    public class LeaderboardsQuickAccessAdminController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboards;

        public LeaderboardsQuickAccessAdminController(ILeaderboardService leaderboards)
        {
            _leaderboards = leaderboards;
        }

        [HttpGet("quickaccess")]
        public Task<List<QuickAccessLeaderboard>> GetQuickAccess()
        {
            return _leaderboards.GetQuickAccessLeaderboards();
        }

        [HttpPost("quickaccess")]
        public Task PostQuickAccess([FromBody]QuickAccessLeaderboard leaderboard)
        {
            return _leaderboards.AddQuickAccessLeaderboard(leaderboard);
        }

        [HttpDelete("quickaccess/{boardName}")]
        public Task DeleteQuickAccess(string boardName)
        {
            return _leaderboards.RemoveQuickAccessLeaderboard(boardName);
        }
    }
}
