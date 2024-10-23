using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports.Admin
{
    class AdminWebApiConfig : IAdminWebApiConfig
    {
        public void ConfigureApplicationParts(ApplicationPartManager apm)
        {
            apm.ApplicationParts.Add(new AssemblyPart(this.GetType().Assembly));
        }
    }

    /// <summary>
    /// Provides Admin API to manage player reports.
    /// </summary>
    [Route("_playerReports")]
    public class PlayerReportsAdminController : ControllerBase
    {

        private readonly DbContextAccessor _contextAccessor;

        /// <summary>
        /// Creates a new instance of <see cref="PlayerReportsAdminController"/>.
        /// </summary>
        /// <param name="contextAccessor"></param>
        public PlayerReportsAdminController(DbContextAccessor contextAccessor)
        {

            this._contextAccessor = contextAccessor;
        }

        /// <summary>
        /// Gets player reports
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<GetPlayerReportsResult> Index(int skip = 0, int size = 50)
        {
            var context = await _contextAccessor.GetDbContextAsync();

            var total = await context.Set<PlayerReport>().CountAsync();
            var reports = await context.Set<PlayerReport>().Skip(skip).Take(size).OrderByDescending(k => k.CreatedOn).Include(r => r.Reported).Include(r => r.Reporter).ToListAsync();

            return new GetPlayerReportsResult
            {
                Reports = reports.Select(r => new PlayerReportSummary
                {
                    CreatedOn = r.CreatedOn,
                    Message = r.Message,
                    ReporterId = r.Reporter.Id,
                    ReportedId = r.Reported.Id,
                    Context = JToken.Parse(JsonSerializer.Serialize(r.Context))
                }),
                Skip = skip,
                Total = total
            };
        }


    }

    /// <summary>
    /// Result of a get player reports request.
    /// </summary>
    public class GetPlayerReportsResult
    {
        /// <summary>
        /// Gets the total number of reports in the DB.
        /// </summary>
        public required int Total { get; init; }

        /// <summary>
        /// Gets the number of reports before the first report in the result.
        /// </summary>
        public required int Skip { get; init; }

        /// <summary>
        /// Gets the list of reports in the result.
        /// </summary>
        public required IEnumerable<PlayerReportSummary> Reports { get; init; }
    }

    /// <summary>
    /// Summary of a player report.
    /// </summary>
    public class PlayerReportSummary
    {
        public DateTime CreatedOn { get; internal set; }
        public string Message { get; internal set; }
        public Guid ReporterId { get; internal set; }
        public Guid ReportedId { get; internal set; }
        public JToken Context { get; internal set; }
    }

}
