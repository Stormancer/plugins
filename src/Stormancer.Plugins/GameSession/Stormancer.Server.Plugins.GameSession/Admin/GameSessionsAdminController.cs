using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
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
        private readonly ISceneHost _scene;

        public GameSessionsAdminController(ISceneHost scene) 
        {
            _scene = scene;
        }

        public async Task<QueryGameSessionsResponse> QueryGamesessions([FromBody] GameSessionsQuery query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets Game session server logs if they are available.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <returns></returns>
        public async Task<ActionResult<GetGameSessionLogsResult>> GetGameSessionServerLogs(string gameSessionId)
        {
            await using var request = _scene.CreateRequestScope();

            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Result of <see cref="GameSessionsAdminController.GetGameSessionServerLogs(string)"/>
    /// </summary>
    public class GetGameSessionLogsResult
    {
        /// <summary>
        /// Gets the id of the game session.
        /// </summary>
        public string GameSessionId { get; set; } = default!;
        /// <summary>
        /// Gets the logs of the game session server.
        /// </summary>
        public IEnumerable<string> Logs { get; set; } = default!;
    }
}
