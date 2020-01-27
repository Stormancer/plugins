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

using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    class LeaderboardController : ControllerBase
    {
        private readonly ILeaderboardService _leaderboard;

        public LeaderboardController(ILeaderboardService leaderboard)
        {
            _leaderboard = leaderboard;
        }

        public async Task Query(RequestContext<IScenePeerClient> ctx)
        {
            var query = ctx.ReadObject<LeaderboardQuery>();
            if (query.Count <= 0)
            {
                query.Count = 10;
            }
            var result = await _leaderboard.Query(query);
            var rankings = result.Results.Select(v => new LeaderboardRanking<ScoreDto>() { Ranking = v.Ranking, Document = new ScoreDto(v.Document) }).ToList();
            var dto = new LeaderboardResult<ScoreDto>() { LeaderboardName = result.LeaderboardName, Next = result.Next, Previous = result.Previous, Results = rankings, Total = result.Total };
            await ctx.SendValue(dto);
        }

        public async Task Cursor(RequestContext<IScenePeerClient> ctx)
        {
            var cursor = ctx.ReadObject<string>();
            var result = await _leaderboard.QueryCursor(cursor);
            var rankings = result.Results.Select(v => new LeaderboardRanking<ScoreDto>() { Ranking = v.Ranking, Document = new ScoreDto(v.Document) }).ToList();
            var dto = new LeaderboardResult<ScoreDto>() { LeaderboardName = result.LeaderboardName, Next = result.Next, Previous = result.Previous, Results = rankings, Total = result.Total };
            await ctx.SendValue(dto);
        }

        public async Task GetRanking(RequestContext<IScenePeerClient> ctx)
        {
            var query = ctx.ReadObject<LeaderboardQuery>();
            query.Count = 1;
            LeaderboardResult<ScoreRecord> result;
            try
            {
                result = await _leaderboard.Query(query);
            }
            catch (Exception)
            {
                result = new LeaderboardResult<ScoreRecord>();
                result.LeaderboardName = query.Name;
            }
            var rankings = result.Results.Select(v => new LeaderboardRanking<ScoreDto>() { Ranking = v.Ranking, Document = new ScoreDto(v.Document) }).ToList();
            var dto = new LeaderboardResult<ScoreDto>() { LeaderboardName = result.LeaderboardName, Next = result.Next, Previous = result.Previous, Results = rankings, Total = result.Total };
            await ctx.SendValue(dto);
        }
    }
}
