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
using Newtonsoft.Json;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{
    class GameHistoryService : IGameHistoryService
    {
        private const string _databaseName = "gamehistory";
        private readonly IESClientFactory _clientFactory;

        public GameHistoryService(
            IESClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        private static readonly ConcurrentDictionary<string, bool> _initializedIndices = new ConcurrentDictionary<string, bool>();

        private async Task<Nest.IElasticClient> CreateClient<T>(string historyType = "")
        {
            var result = await _clientFactory.CreateClient<T>(_databaseName, historyType);

            if (_initializedIndices.TryAdd("", true))
            {
                await CreateGameHistoryMapping(result);
            }
            return result;
        }

        private Task CreateGameHistoryMapping(Nest.IElasticClient client)
        {
            return client.MapAsync<GameHistoryRecord>(m =>
                m.DynamicTemplates(templates => templates
                    .DynamicTemplate("dates", t => t
                         .Match("CreatedOn")
                         .Mapping(ma => ma.Date(s => s))
                        )
                     .DynamicTemplate("score", t =>
                        t.MatchMappingType("string")
                         .Mapping(ma => ma.Keyword(s => s.Index()))
                         )
                    )
            );
        }

        public async Task AddToHistory<T>(IEnumerable<T> items) where T : HistoryRecord
        {
            var client = await CreateClient<T>();

            await client.BulkAsync(d => d.IndexMany(items, (bd, item) => bd.Id(item.Id)));
        }

        public async Task<GameHistoryRecord> GetGameHistory(string gameId)
        {
            var client = await CreateClient<GameHistoryRecord>();
            var result = await client.GetAsync<GameHistoryRecord>(gameId);

            return result.Source;
        }

        public async Task<GameHistorySearchResult<PlayerHistoryRecord>> GetPlayerHistory(string playerId, int count)
        {
            var client = await CreateClient<PlayerHistoryRecord>();
            var result = await client.SearchAsync<PlayerHistoryRecord>(s => s
                .Query(q => q.Bool(b => b.Must(m => m.Term("playerId.keyword", playerId))))
                .Size(count + 1)
                .Sort(sort => sort.Descending(r => r.GameEndedOn))
            );

            return new GameHistorySearchResult<PlayerHistoryRecord>
            {
                Documents = result.Documents.Take(count),
                Next = result.Documents.Count() == count + 1 ? GetNextCursor(result.Documents, count) : "",
                Previous = ""
            };
        }

        public async Task<GameHistorySearchResult<PlayerHistoryRecord>> GetPlayerHistoryByCursor(string cursor)
        {
            var cData = ReadCursor(cursor);
            var client = await CreateClient<PlayerHistoryRecord>();
            var result = await client.SearchAsync<PlayerHistoryRecord>(s => s
                .Query(q => q.Bool(b => b.Must(
                    m => m.Term("playerId", cData.Id),
                    m => m.DateRange(dr =>
                    {
                        switch (cData.Type)
                        {
                            case CursorType.Next:
                                dr = dr.LessThan(cData.PivotDate);
                                break;
                            case CursorType.Previous:
                                dr = dr.GreaterThan(cData.PivotDate);
                                break;
                            default:
                                break;
                        }
                        return dr;
                    }))))
                .Size(cData.Count + 1)
                .Sort(sort =>
                    {
                        switch (cData.Type)
                        {
                            case CursorType.Next:
                                sort = sort.Descending(r => r.GameStartedOn);
                                break;
                            case CursorType.Previous:
                                sort = sort.Ascending(r => r.GameStartedOn);
                                break;
                        }
                        return sort;
                    })
            );
            var documents = cData.Type == CursorType.Next ? result.Documents : result.Documents.Reverse();

            return new GameHistorySearchResult<PlayerHistoryRecord>
            {
                Documents = documents.Take(cData.Count),
                Next = result.Documents.Count() == cData.Count + 1 ? GetNextCursor(result.Documents, cData.Count) : "",
                Previous = result.Documents.Count() == cData.Count + 1 ? GetPreviousCursor(result.Documents, cData.Count) : ""
            };
        }

        public async Task<GameHistorySearchResult<GameHistoryRecord>> GetGameHistoryForPlayer(string playerId, int count)
        {
            var client = await CreateClient<GameHistoryRecord>();
            var result = await client.SearchAsync<GameHistoryRecord>(s => s
                .Query(q => q.Bool(b => b.Must(m => m.Term("players.id.keyword", playerId))))
                .Size(count + 1)
                .Sort(sort => sort.Descending(r => r.GameEndedOn))
            );

            return new GameHistorySearchResult<GameHistoryRecord>
            {
                Documents = result.Documents.Take(count),
                Next = result.Documents.Count() == count + 1 ? GetNextCursor(result.Documents, count) : "",
                Previous = "",
                Total = result.Total
            };
        }

        public async Task<GameHistorySearchResult<GameHistoryRecord>> GetGameHistoryForPlayer(string cursor)
        {
            var cData = ReadCursor(cursor);
            var client = await CreateClient<GameHistoryRecord>();
            var result = await client.SearchAsync<GameHistoryRecord>(s => s
                .Query(q => q.Bool(b => b.Must(
                    m => m.Term("players.id.keyword", cData.Id),
                    m => m.DateRange(dr =>
                    {
                        switch (cData.Type)
                        {
                            case CursorType.Next:
                                dr = dr.LessThan(cData.PivotDate);
                                break;
                            case CursorType.Previous:
                                dr = dr.GreaterThan(cData.PivotDate);
                                break;
                            default:
                                break;
                        }
                        return dr;
                    }))))
                .Size(cData.Count + 1)
                .Sort(sort =>
                {
                    switch (cData.Type)
                    {
                        case CursorType.Next:
                            sort = sort.Descending(r => r.GameStartedOn);
                            break;
                        case CursorType.Previous:
                            sort = sort.Ascending(r => r.GameStartedOn);
                            break;
                    }
                    return sort;
                })
            );
            var documents = cData.Type == CursorType.Next ? result.Documents : result.Documents.Reverse();

            return new GameHistorySearchResult<GameHistoryRecord>
            {
                Documents = documents.Take(cData.Count),
                Next = result.Documents.Count() == cData.Count + 1 ? GetNextCursor(result.Documents, cData.Count) : "",
                Previous = result.Documents.Count() == cData.Count + 1 ? GetPreviousCursor(result.Documents, cData.Count) : "",
                Total = result.Total
            };
        }

        private enum CursorType
        {
            Previous,
            Next
        }

        private class Cursor
        {
            public CursorType Type { get; set; }

            public string Id { get; set; }

            public DateTime PivotDate { get; set; }

            public int Count { get; set; }

            public Type RecordType { get; internal set; }
        }

        /// <summary>
        /// Create previous cursor
        /// </summary>
        /// <param name="historySet"> set of results to build the cursor from, ordered by CreationDate descending</param>
        /// <returns></returns>
        private string GetPreviousCursor<T>(IEnumerable<T> historySet, int count) where T : HistoryRecord
        {
            var first = historySet.FirstOrDefault();
            if (first == null)
            {
                return "";
            }

            var cursor = new Cursor { Count = count, PivotDate = first.GameStartedOn, Id = first.Id, Type = CursorType.Previous, RecordType = typeof(T) };

            var json = JsonConvert.SerializeObject(cursor);

            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// Create next cursor
        /// </summary>
        /// <param name="historySet"> set of results to build the cursor from, ordered by CreationDate descending</param>
        /// <returns></returns>
        private string GetNextCursor<T>(IEnumerable<T> historySet, int count) where T : HistoryRecord
        {
            var first = historySet.LastOrDefault();
            if (first == null)
            {
                return "";
            }

            var cursor = new Cursor { Count = count, PivotDate = first.GameStartedOn, Id = first.Id, Type = CursorType.Next, RecordType = typeof(T) };

            var json = JsonConvert.SerializeObject(cursor);

            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
        }

        private Cursor ReadCursor(string cursorString)
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursorString));
            return JsonConvert.DeserializeObject<Cursor>(json);
        }

        public Task<IElasticClient> GetESClient<T>(string historyType) where T : HistoryRecord
        {
            return CreateClient<T>(historyType);
        }
    }
}
