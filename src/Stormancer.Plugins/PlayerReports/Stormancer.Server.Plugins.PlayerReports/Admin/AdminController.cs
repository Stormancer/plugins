using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Nest;
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
    /// <summary>
    /// Possible sort values for reports.
    /// </summary>
    public enum PlayerReportSortedByValues
    {
        /// <summary>
        /// Sort by last report date
        /// </summary>
        LastReportedOn,

        /// <summary>
        /// Sort by total number of reports
        /// </summary>
        TotalReports,

        /// <summary>
        /// Sort by number of reports by distinct reporters.
        /// </summary>
        DistinctTotalReports,
    }
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
        /// <param name="sortedBy"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<GetPlayerReportsResult> Index(int skip = 0, int size = 50, PlayerReportSortedByValues sortedBy = PlayerReportSortedByValues.LastReportedOn)
        {
            var context = await _contextAccessor.GetDbContextAsync();

            var total = await context.Set<PlayerReport>().GroupBy(r => r.ReportedId).Select(r => r.Key).CountAsync();
            var reportsQuery = context.Set<PlayerReport>().GroupBy(r => r.ReportedId).Select(g => new
            {
                ReportedUserId = g.Key,
                TotalReports = g.Count(),
                TotalDistinctReporters = g.GroupBy(r => r.ReporterId).Count(),
                LastReportedOn = g.Max(r => r.CreatedOn)
            });

            var reports = await (sortedBy switch
            {
                PlayerReportSortedByValues.LastReportedOn => reportsQuery.OrderByDescending(r => r.LastReportedOn),
                PlayerReportSortedByValues.TotalReports => reportsQuery.OrderByDescending(r=> r.TotalReports),
                PlayerReportSortedByValues.DistinctTotalReports => reportsQuery.OrderByDescending(r=>r.TotalDistinctReporters),
                _ => reportsQuery
            }).Skip(skip).Take(size).ToListAsync();


            return new GetPlayerReportsResult
            {
                Reports = reports.Select(r => new PlayerReportSummary
                {
                    ReportedUserId = r.ReportedUserId.ToString("N"),
                    TotalDistinctReporters = r.TotalDistinctReporters,
                    TotalReports = r.TotalReports,
                    LastReportedOn = r.LastReportedOn,
                }),
                Skip = skip,
                Total = total
            };
        }

        [HttpGet]
        [Route("{reportedUserId}")]
        public async Task<IEnumerable<PlayerReportDetails>> GetDetails(string reportedUserId)
        {
            var guid = Guid.Parse(reportedUserId);

            var context = await _contextAccessor.GetDbContextAsync();
            var result = await context
                .Set<PlayerReport>()
                .Where(r => r.ReportedId == guid)
                .OrderByDescending(r=>r.CreatedOn)
                .ToListAsync();

            return result.Select(r => new PlayerReportDetails
            {
                Context = JToken.Parse(JsonSerializer.Serialize(r.Context)),
                CreatedOn = r.CreatedOn,
                Message = r.Message,
                ReportedUserId = r.ReportedId.ToString("N"),
                ReporterUserId = r.ReporterId.ToString("N"),
            });
            //CreatedOn = r.CreatedOn,
            //        Message = r.Message,
            //        ReporterId = r.Reporter.Id,
            //        ReportedId = r.Reported.Id,
            //        Context = JToken.Parse(JsonSerializer.Serialize(r.Context))
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
    /// Summary information about an user who have been reported in the game.
    /// </summary>
    public class PlayerReportSummary
    {
        /// <summary>
        /// IId of the user being reported.
        /// </summary>
        public required string ReportedUserId { get; init; }

        /// <summary>
        /// Total count of reports against the user.
        /// </summary>
        public required int TotalReports { get; init; }

        /// <summary>
        /// Total count of distinct users who reported the user.
        /// </summary>
        public required int TotalDistinctReporters { get; init; }

        /// <summary>
        /// The date time the user was last reported on.
        /// </summary>
        public required DateTime LastReportedOn { get; init; }
    }

    /// <summary>
    /// Summary of a player report.
    /// </summary>
    public class PlayerReportDetails
    {
        /// <summary>
        /// Date the reports was created on (UTC)
        /// </summary>
        public required DateTime CreatedOn { get; init; }

        /// <summary>
        /// Report message
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Id of the user who created the report.
        /// </summary>
        public required string ReporterUserId { get; init; }

        /// <summary>
        /// Id of the user being reported.
        /// </summary>
        public required string ReportedUserId { get; init; }

        /// <summary>
        /// Additional data in the report.
        /// </summary>
        public required JToken Context { get; init; }
    }

}
