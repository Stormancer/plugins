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

using Microsoft.EntityFrameworkCore;
using Nest;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.GameHistory;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends.RecentlyMet
{
    class RecentTeam
    {
        public string Id { get; set; } = default!;
        public DateTimeOffset Date { get; set; }
        public List<string> UserIds { get; set; } = default!;
    }

    class RecentlyMetUsersEventHandler : IFriendsEventHandler
    {

        private readonly IFriendsService _friends;
        private readonly DbContextAccessor _dbAccessor;
        private int _maxRecentlyMet = 10;

        public RecentlyMetUsersEventHandler(IFriendsService friends, DbContextAccessor dbAccessor, ILogger logger)
        {
            _friends = friends;
            _dbAccessor = dbAccessor;
        }



        public async Task OnGetFriends(GetFriendsCtx getMetUsersCtx)
        {
            var userId = Guid.Parse(getMetUsersCtx.UserId);
            var ctx = await _dbAccessor.GetDbContextAsync();

            var currentFriends = getMetUsersCtx.Friends.Select(f => Guid.Parse(f.UserId)).ToList();
            currentFriends.Add(userId);
            var query = from userHistory in ctx.Set<UserGameHistoryRecord>()
                        join historyRecord in ctx.Set<GameHistoryRecord>()
                        on userHistory.GameHistoryRecordId equals historyRecord.Id
                        join otherHistory in ctx.Set<UserGameHistoryRecord>()
                        on historyRecord.Id equals otherHistory.GameHistoryRecordId
                        where userHistory.UserRecordId == userId && !currentFriends.Contains(otherHistory.UserRecordId)
                        orderby historyRecord.CreatedOn descending
                        select new
                        {
                            date = historyRecord.CreatedOn,
                            userId = otherHistory.UserRecordId,
                            
                        };

            var recentlyPlayedWith = await query.Distinct().Take(_maxRecentlyMet).ToListAsync();

            foreach (var friendId in recentlyPlayedWith)
            {
                getMetUsersCtx.Friends.Add(new Friend { UserId = userId.ToString(), Status = FriendConnectionStatus.Disconnected, Tags = new List<string> { "recentlyMet" } });
            }
        }
    }
}
