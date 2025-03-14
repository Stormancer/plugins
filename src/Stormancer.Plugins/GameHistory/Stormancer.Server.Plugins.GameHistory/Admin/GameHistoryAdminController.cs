using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{

    /// <summary>
    /// Admin Web API controller for game history.
    /// </summary>
    [ApiController]
    [Route("_gameshistory")]
    public class GameHistoryAdminController : ODataController
    {
        private readonly DbContextAccessor _dbContextAccessor;

        /// <summary>
        /// Creates a new <see cref="GameHistoryAdminController"/> object.
        /// </summary>
        /// <param name="dbContextAccessor"></param>
        public GameHistoryAdminController(DbContextAccessor dbContextAccessor)
        {
            this._dbContextAccessor = dbContextAccessor;
        }


        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<GameHistoryItem>>> Get(ODataQueryOptions<GameHistoryRecord> options)
        {
            var dbContext = await _dbContextAccessor.GetDbContextAsync();

            var result = (IQueryable<GameHistoryRecord>)(options.ApplyTo(dbContext.Set<GameHistoryRecord>().Include(r => r.Participants)));
            return Ok((await result.ToListAsync()).Select(r => new GameHistoryItem
            {
                Participants = r.Participants.Select(p => p.Id),
                CompletedOn = r.CompletedOn,
                CustomData = JToken.Parse(JsonSerializer.Serialize(r.CustomData)),
                CreatedOn = r.CreatedOn,
                Id = r.Id
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GameHistoryItem>> Details(Guid id)
        {
            var dbContext = await _dbContextAccessor.GetDbContextAsync();
            var r = await dbContext.Set<GameHistoryRecord>().FindAsync(id);
            return Ok(new GameHistoryItem
            {
                Participants = r.Participants.Select(p => p.Id),
                CompletedOn = r.CompletedOn,
                CustomData = JToken.Parse(JsonSerializer.Serialize(r.CustomData)),
                CreatedOn = r.CreatedOn,
                Id = r.Id
            });
        }
    }

    public class GameHistoryItem
    {
        public required IEnumerable<Guid> Participants { get; init; }
        public required DateTime CompletedOn { get; init; }

        public required JToken CustomData { get; init; }
        public required DateTime CreatedOn { get; init; }
        public required Guid Id { get; init; }
    }


}
