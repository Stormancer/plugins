using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        [EnableQuery]
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<GameHistoryRecord>>> Get()
        {
            var dbContext = await _dbContextAccessor.GetDbContextAsync();

            return Ok(dbContext.Set<GameHistoryRecord>().Include(r => r.Participants));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GameHistoryRecord>> Details(Guid id)
        {
            var dbContext = await _dbContextAccessor.GetDbContextAsync();
            return Ok(await dbContext.Set<GameHistoryRecord>().FindAsync(id));
        }
    }

   
}
