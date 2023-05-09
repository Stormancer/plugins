using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.Admin
{
    public class GameSessionDocument
    {

    }
    public class QueryGameSessionsResponse
    {
        public IEnumerable<GameSessionDocument> Gamesessions { get; set; }
        public uint Total { get; set; }
    }

    public class GameSessionsQuery
    {
        public JObject Filter { get; set; }
        public uint Skip { get; set; } = 0;
        public uint Size { get; set; } = 20;
    }
    public class GameSessionsAdminController
    {
        public GameSessionsAdminController() 
        { 
        }

        public async Task<QueryGameSessionsResponse> QueryGamesessions([FromBody] GameSessionsQuery query)
        {
            throw new NotImplementedException();
        }
    }
}
