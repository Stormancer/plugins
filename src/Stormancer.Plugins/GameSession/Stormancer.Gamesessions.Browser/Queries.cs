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
using Stormancer.Server.Plugins.Models;
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

    /// <summary>
    /// A filter for queries in the <see cref="GamesessionsDocumentStore"/>.
    /// </summary>
    [MessagePackObject]
    public class GamesessionsDocumentStoreFilter
    {
        /// <summary>
        /// Set of <see cref="PartySummary"/> objects representing the parties to try to fit in game sessions.
        /// </summary>
        [Key(0)]
        public required IEnumerable<PartySummary> PartiesToFit{ get; init; }

        /// <summary>
        /// A boolean indicating if splitting parties is authorized to make them fit in the game session.
        /// </summary>
        [Key(1)]
        public bool AcceptSplit { get; set; } = false;
    }

    /// <summary>
    /// Document representing a game session in a query response.
    /// </summary>
    [MessagePackObject]
    public class GamesessionDocumentSource
    {
        /// <summary>
        /// Settings of the game session.
        /// </summary>
        [Key(0)]
        public required IReadOnlyDictionary<string, string> Settings { get; init; }

        /// <summary>
        /// Teams configuration of the game session.
        /// </summary>
        [Key(1)]
        public required IEnumerable<TeamConfigurationRecord> Teams { get; init; }

        /// <summary>
        /// Number of players currently in the game session, including any reservations.
        /// </summary>
        [Key(2)]
        public required int PlayerCount { get; set; }

        /// <summary>
        /// The date the game session was created on.
        /// </summary>
        [Key(3)]
        public required DateTime CreatedOn { get; init; }

        /// <summary>
        /// Name of the game session.
        /// </summary>
        [Key(4)]
        public required string Name { get; init; }
    }

    /// <summary>
    /// A queriable store containing <see cref="IGameSessionService"/> objects.
    /// </summary>
    public class GamesessionsDocumentStore : IServiceSearchProvider
    {
        private object _syncRoot = new object();
        private readonly Dictionary<string, IGameSessionService> _store = [];

        /// <summary>
        /// Id of the store.
        /// </summary>
        public const string GAMESESSION_SEARCH_TYPE = "stormancer.gamesessions";
        SearchResult<JObject> IServiceSearchProvider.Filter(string type, JObject filter, uint size)
        {
            var result = new SearchResult<JObject>();
            if (type != GAMESESSION_SEARCH_TYPE)
            {
                return result;
            }
            
            var gsFilter = filter.ToObject<GamesessionsDocumentStoreFilter>();
            if(gsFilter == null)
            {
                return new SearchResult<JObject>();
            }

            var docs = new List<Document<JObject>>();
            uint total = 0;
            lock (_syncRoot)
            {
                foreach (var (id, gs) in _store)
                {
                    if (gs.CanFit(gsFilter.PartiesToFit, gsFilter.AcceptSplit))
                    {
                        total++;
                        if (docs.Count < size)
                        {
                            docs.Add(new Document<JObject>(id, JObject.FromObject(new GamesessionDocumentSource
                            {
                                Settings = gs.GetSettings(),
                                Teams = gs.GetTeamsConfiguration(),
                                CreatedOn = gs.CreatedOn,
                                PlayerCount = gs.PlayerCount,
                                Name = gs.Name
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

        /// <summary>
        /// Adds a gamesession represented by its <see cref="IGameSessionService"/> to the store.
        /// </summary>
        /// <param name="gameSession"></param>
        public void Add(IGameSessionService gameSession)
        {
            lock (_syncRoot)
            {
                _store.Add(gameSession.GameSessionId, gameSession);
            }
        }

        /// <summary>
        /// Removes a previously added <see cref="IGameSessionService"/> from the store.
        /// </summary>
        /// <param name="gameSession"></param>
        public void Remove(IGameSessionService gameSession)
        {
            lock (_syncRoot)
            {
                _store.Remove(gameSession.GameSessionId);
            }
        }

        /// <summary>
        /// Gets game sessions.
        /// </summary>
        public IEnumerable<IGameSessionService> GameSessions
        {
            get
            {
                lock (_syncRoot)
                {
                    return _store.Values.ToList();
                }
            }
        }
    }
}
