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

using MessagePack;
using Nest;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Gamesessions.Browser
{
    /// <summary>
    /// Provides method to search for parties.
    /// </summary>
    public class GamesessionSearchService
    {
        private readonly SearchEngine search;

        /// <summary>
        /// Creates a new instance of the service.
        /// </summary>
        /// <param name="search"></param>
        public GamesessionSearchService(SearchEngine search)
        {
            this.search = search;
        }

        /// <summary>
        /// Search for parties.
        /// </summary>
        /// <param name="query">Query object</param>
        /// <param name="skip"></param>
        /// <param name="size"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<SearchResult<T>> SearchGamesessions<T>(JObject query, uint skip, uint size, CancellationToken cancellationToken = default)
        {
            return search.QueryAsync<T>(GamesessionsDocumentStore.GAMESESSION_SEARCH_TYPE, query, skip, size, cancellationToken);
        }


    }

    [MessagePackObject]
    public class GamesessionsDocumentStoreFilter
    {
        public int PartySize { get; set; } = 1;
        public bool AcceptSplit { get; set; } = false;
    }

    [MessagePackObject]
    public class GamesessionDocumentSource
    {
        [Key(0)]
        public required IReadOnlyDictionary<string, string> Settings { get; init; }


        [Key(1)]
        public required IEnumerable<TeamConfigurationRecord> Teams { get; init; }


        [Key(2)]
        public required int PlayerCount { get; init; }


        [Key(3)]
        public required DateTime CreatedOn { get; init; }
    }
    public class GamesessionsDocumentStore : IServiceSearchProvider
    {
        private object _syncRoot = new object();
        private readonly Dictionary<string, IGameSessionService> _store = [];
        public const string GAMESESSION_SEARCH_TYPE = "stormancer.gamesessions";
        SearchResult<JObject> IServiceSearchProvider.Filter(string type, JObject filter, uint size)
        {
            var result = new SearchResult<JObject>();
            if (type != GAMESESSION_SEARCH_TYPE)
            {
                return result;
            }
            var gsFilter = filter.ToObject<GamesessionsDocumentStoreFilter>() ?? new GamesessionsDocumentStoreFilter();
            var docs = new List<Document<JObject>>();
            uint total = 0;
            lock (_syncRoot)
            {
                foreach (var (id, gs) in _store)
                {
                    if (gs.CanFit(gsFilter.PartySize, gsFilter.AcceptSplit))
                    {
                        total++;
                        if (docs.Count < size)
                        {
                            docs.Add(new Document<JObject>(id, JObject.FromObject(new GamesessionDocumentSource
                            {
                                Settings = gs.GetSettings(),
                                Teams = gs.GetTeamsConfiguration(),
                                CreatedOn = gs.CreatedOn,
                                PlayerCount = gs.PlayerCount
                            }))
                            { Version = 1 });
                        }
                    }
                }
            }
            result.Total = total;
            result.Hits = docs;
            return result;
        }

        bool IServiceSearchProvider.Handles(string type)
        {
            return type == GAMESESSION_SEARCH_TYPE;
        }

        public void Add(IGameSessionService gameSession)
        {
            lock (_syncRoot)
            {
                _store.Add(gameSession.GameSessionId, gameSession);
            }
        }
        public void Remove(IGameSessionService gameSession)
        {
            lock (_syncRoot)
            {
                _store.Remove(gameSession.GameSessionId);
            }
        }

    }

    //internal class GamesessionLuceneDocumentStore : ILuceneDocumentStore
    //{
    //    public const string PARTY_LUCENE_INDEX = "stormancer.gamesessions";

    //    private readonly ILucene lucene;
    //    private Dictionary<string, (JObject, byte[])> _data = new Dictionary<string, (JObject, byte[])>();
    //    private object syncRoot = new object();
    //    public GamesessionLuceneDocumentStore(ILucene lucene)
    //    {
    //        this.lucene = lucene;
    //    }
    //    public IEnumerable<Document<JObject>> GetDocuments(IEnumerable<string> ids)
    //    {

    //        foreach (var id in ids)
    //        {
    //            lock (syncRoot)
    //            {

    //                if (_data.TryGetValue(id, out var doc))
    //                {
    //                    yield return new Document<JObject>(id, doc.Item1) { Version = 1 };
    //                }
    //                else
    //                {
    //                    yield return new Document<JObject>(id, default) { Version = 1 };
    //                }
    //            }
    //        }
    //    }

    //    public bool Handles(string type)
    //    {
    //        return type == PARTY_LUCENE_INDEX;
    //    }

    //    public void Initialize()
    //    {
    //        lucene.TryCreateIndex(PARTY_LUCENE_INDEX, DefaultMapper.JsonMapper);
    //    }

    //    public void UpdateDocument<T>(string id, T? document, byte[] customData)
    //    {
    //        if (document != null)
    //        {
    //            var json = JObject.FromObject(document);
    //            lock (syncRoot)
    //            {

    //                if (!_data.TryGetValue(id, out var current) || !JToken.DeepEquals(json, current.Item1))
    //                {

    //                    lucene.IndexDocument(PARTY_LUCENE_INDEX, id, json);
    //                }
    //                _data[id] = (json, customData);
    //            }
    //        }
    //        else
    //        {
    //            DeleteDocument(id);

    //        }
    //    }

    //    public void DeleteDocument(string id)
    //    {
    //        var mustRemove = false;
    //        lock (syncRoot)
    //        {
    //            mustRemove = _data.Remove(id);

    //        }
    //        if (mustRemove)
    //        {
    //            lucene.DeleteDocument(PARTY_LUCENE_INDEX, id);
    //        }


    //    }
    //}
}
