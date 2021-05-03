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

using Nest;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database;
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

    class RecentlyMetUsersEventHandler : IFriendsEventHandler, IGameSessionEventHandler
    {
        private const string INDEX_NAME = "RecentlyPlayedWith";
        private readonly IFriendsService _friends;
        private Task<IElasticClient> _client;
        private int _maxRecentlyMet = 10;

        public RecentlyMetUsersEventHandler(IFriendsService friends, IESClientFactory esClient, ILogger logger)
        {
            _friends = friends;
            _client = esClient.CreateClient<RecentTeam>(INDEX_NAME);
        }

        public async Task GameSessionStarted(GameSessionStartedCtx ctx)
        {
            var client = await _client;

            var tasks = new List<Task>();

            var date = DateTimeOffset.UtcNow;

            foreach (var team in ctx.Config.Teams)
            {
                var recentTeam = new RecentTeam { Id = Guid.NewGuid().ToString("N"), Date = date, UserIds = team.Parties.SelectMany(g => g.Players.Keys).ToList() };
                tasks.Add(client.IndexDocumentAsync(recentTeam));
                foreach (var userId in recentTeam.UserIds)
                {
                    var friends = recentTeam.UserIds.Where(id => id != userId).Select(id => new Friend { UserId = id, LastConnected = date, Status = FriendStatus.Disconnected, Tags = new List<string> { "recentlyMet" } });
                    tasks.Add(_friends.AddNonPersistedFriends(userId, friends, CancellationToken.None));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task OnGetFriends(GetFriendsCtx getMetUsersCtx)
        {
            var client = await _client;
            var result = await client.SearchAsync<RecentTeam>(sd => sd
                .Size(_maxRecentlyMet)
                .Sort(ss => ss.Descending(dd => dd.Date))
                .Query(qu => qu.Terms(termsDesc => termsDesc.Field(fieldDesc => fieldDesc.UserIds).Terms(getMetUsersCtx.UserId)))
            );

            var userIds = result.Documents.SelectMany(d => d.UserIds.Select(u => new { id = u, date = d.Date })).DistinctBy(d => d.id).Where(d => d.id != getMetUsersCtx.UserId).Take(_maxRecentlyMet);
            foreach (var userId in userIds)
            {
                getMetUsersCtx.Friends.Add(new Friend { LastConnected = userId.date, UserId = userId.id, Status = FriendStatus.Disconnected, Tags = new List<string> { "recentlyMet" } });
            }
        }

      
    }

    internal static class EnumerableExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> knownKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (knownKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
