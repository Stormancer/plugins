using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Queries;
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
        public async Task<SearchResult<GamesessionDocumentSource>> Search(string jsonQuery, uint skip, uint size, CancellationToken cancellationToken)
        {
            var result = await _gamesessionSearchService.SearchGamesessions<JObject>(new JObject(), skip, size, cancellationToken);

            return result.Convert(source => source?.ToObject<GamesessionDocumentSource>());
        }


    }


}