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
            var result = await _gamesessionSearchService.SearchGamesessions<JObject>(JObject.Parse(jsonQuery), skip, size, cancellationToken);

            return new GamesessionSearchResultDto { Total = result.Total, Hits = result.Hits.Select(d => new GamesessionSearchDocumentDto { Id = d.Id, Source = d.Source?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}" }) };
        }

        public async Task GetReservations()
        {

        }

        public async Task Cancel()
        {

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