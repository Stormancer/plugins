using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Gamesessions.Browser
{
    public class GamesessionBrowserController : Server.Plugins.API.ControllerBase
    {
        private GamesessionSearchService _gamesessionSearchService;

        internal GamesessionBrowserController(GamesessionSearchService gamesessionSearchService)
        {
            _gamesessionSearchService = gamesessionSearchService;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<GamesessionSearchResultDto> Search(string jsonQuery, uint skip, uint size, CancellationToken cancellationToken)
        {
            var result = await _gamesessionSearchService.SearchGamesessions(JObject.Parse(jsonQuery), skip, size, cancellationToken);

            return new GamesessionSearchResultDto { Total = result.Total, Hits = result.Hits.Select(d => new GamesessionSearchDocumentDto { Id = d.Id, Source = d.Source?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}" }) };
        }

        public async Task GetReservations()
        {

        }

        public async Task Cancel()
        {

        }


    }

    public class GamesessionBrowserDocumentController : Server.Plugins.API.ControllerBase
    {
        private readonly GamesessionLuceneDocumentStore _store;
        private readonly IGameSessionService _gameSession;
        private readonly IUserSessions _sessions;
        private readonly GamesessionSearchState _searchState;

        internal GamesessionBrowserDocumentController(GamesessionLuceneDocumentStore store, IGameSessionService gameSession, IUserSessions sessions, GamesessionSearchState searchState)
        {
            _store = store;
            _gameSession = gameSession;
            _sessions = sessions;
            _searchState = searchState;
        }


        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UpdateDocument(string json, RequestContext<IScenePeerClient> ctx)
        {
            if(ctx.RemotePeer.SessionId != _gameSession.HostSessionId)
            {
                throw new ClientException("notAuthorized");
            }
            var obj = JObject.Parse(json);

            _store.UpdateDocument(_gameSession.GameSessionId, obj, Array.Empty<byte>());
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task RemoveDocument(RequestContext<IScenePeerClient> ctx)
        {
            if (ctx.RemotePeer.SessionId != _gameSession.HostSessionId)
            {
                throw new ClientException("notAuthorized");
            }

            _store.DeleteDocument(_gameSession.GameSessionId);
        }
    }


    /// <summary>
    /// A party search document.
    /// </summary>
    [MessagePackObject]
    public class GamesessionSearchDocumentDto
    {
        /// <summary>
        /// Id of the party.
        /// </summary>
        [Key(0)]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Json Data associated with the party.
        /// </summary>
        [Key(1)]
        public string Source { get; set; } = default!;
    }

    /// <summary>
    /// A party search result.
    /// </summary>
    [MessagePackObject]
    public class GamesessionSearchResultDto
    {
        /// <summary>
        /// Total number of documents returned by the search.
        /// </summary>
        [Key(0)]
        public uint Total { get; set; }

        /// <summary>
        /// Results in the search result.
        /// </summary>
        [Key(1)]
        public IEnumerable<GamesessionSearchDocumentDto> Hits { get; set; } = default!;
    }
}