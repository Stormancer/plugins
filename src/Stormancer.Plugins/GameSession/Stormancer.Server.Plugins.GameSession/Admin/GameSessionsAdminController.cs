using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

    /// <summary>
    /// Monitor game sessions.
    /// </summary>
    [ApiController]
    [Route("_gamesessions")]
    public class GameSessionsAdminController : ControllerBase
    {
        private readonly ISceneHost _scene;

        /// <summary>
        /// Creates a new instance of <see cref="GameSessionsAdminController"/>
        /// </summary>
        /// <param name="scene"></param>
        public GameSessionsAdminController(ISceneHost scene)
        {
            _scene = scene;
        }

        /// <summary>
        /// Gets informations about a game session.
        /// </summary>
        /// <param name="id">Id of the game session</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        [HttpGet]
        [Route("{id}")]
        public async Task<ActionResult<GameSessionStatus>> GetGameSession(string id, CancellationToken cancellationToken)
        {
            await using var scope = _scene.CreateRequestScope();

            var service = scope.Resolve<GameSessionsMonitoringService>();
            return Ok(await service.InspectGameSessionAsync(id, cancellationToken));
        }


        /// <summary>
        /// Gets Game session server logs if they are available.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("{id}/logs")]
        public async Task<ActionResult<GetGameSessionLogsResult>> GetGameSessionServerLogs(string id, CancellationToken cancellationToken)
        {
            await using var scope = _scene.CreateRequestScope();

            var service = scope.Resolve<GameSessionsMonitoringService>();

            var logs = await service.QueryGameServerLogsAsync(id, null, null, 0, false, cancellationToken).ToListAsync();


            return Ok(new GetGameSessionLogsResult { GameSessionId = id, Logs = logs });
        }
    }

    /// <summary>
    /// Result of <see cref="GameSessionsAdminController.GetGameSessionServerLogs(string,CancellationToken)"/>
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
