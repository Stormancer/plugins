using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.BlobStorage;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports.Admin
{
    /// <summary>
    /// Provides Admin API to manage player reports.
    /// </summary>
    [Route("_playerReports")]
    public class BugReportsAdminController : ControllerBase
    {

        private readonly DbContextAccessor _contextAccessor;
        private readonly IBlobStorage _storage;
        private readonly ConfigurationMonitor<BugReportsInternalBackendConfigurationSection> _configuration;

        /// <summary>
        /// Creates a new instance of <see cref="PlayerReportsAdminController"/>.
        /// </summary>
        /// <param name="contextAccessor"></param>
        /// <param name="storage"></param>
        /// <param name="configuration"></param>
        public BugReportsAdminController(DbContextAccessor contextAccessor, IBlobStorage storage, ConfigurationMonitor<BugReportsInternalBackendConfigurationSection> configuration)
        {

            this._contextAccessor = contextAccessor;
            _storage = storage;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets player reports
        /// </summary>
        /// <param name="from"></param>
        /// <param name="skip"></param>
        /// <param name="size"></param>
        /// <param name="sortedBy"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<ActionResult<GetPlayerReportsResult>> Index(DateTimeOffset from, int skip = 0, int size = 50)
        {
            var context = await _contextAccessor.GetDbContextAsync();

            DateTime fromDate = from.ToUniversalTime().DateTime;
            var total = await context.Set<BugReportRecord>().Where(r => r.CreatedOn >= fromDate).CountAsync();
            var reports = await context.Set<BugReportRecord>().Where(r => r.CreatedOn >= fromDate).Skip(skip).Take(size).ToListAsync();


            return Ok(new GetBugReportsResult
            {
                Reports = reports.Select(r => new BugReportSummary
                {
                    ReportedOn = r.CreatedOn,
                    Message = r.Message,
                    Metadata = JToken.Parse(JsonSerializer.Serialize(r.Context)),
                    ReporterUserId = r.ReporterId.ToString("N"),
                    Attachments = GetAttachments(r.Attachements)
                }),
                Skip = skip,
                Total = total
            });
        }

        private IEnumerable<BugReportAttachmentSummary> GetAttachments(JsonDocument json)
        {
            var records = json.Deserialize<IEnumerable<BugReportAttachementRecord>>();

            if (records != null)
            {
                foreach (var record in records)
                {
                    yield return new BugReportAttachmentSummary { ContentType = record.ContentType, AttachmentId = record.AttachmentId };
                }
            }

        }


        /// <summary>
        /// Gets bug reports attachments.
        /// </summary>
        /// <param name="reportId"></param>
        /// <param name="attachmentId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("attachments/{reportId}/{attachmentId")]
        [ActionName("attachment")]
        public async Task<ActionResult> GetAttachment(string reportId, string attachmentId)
        {
            var storageId = _configuration.Value.AttachmentStorageId;

            var content = await _storage.GetBlobContent($"{storageId}/bugReports/{reportId}/{attachmentId}");

            if(content.Success)
            {
                return new FileStreamResult(content.Content, content.ContentType);
            }
            else
            {
                return NotFound();
            }
        }


    }

    /// <summary>
    /// Result of a get player reports request.
    /// </summary>
    public class GetBugReportsResult
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
        public required IEnumerable<BugReportSummary> Reports { get; init; }
    }

    /// <summary>
    /// Summary information about an user who have been reported in the game.
    /// </summary>
    public class BugReportSummary
    {
        /// <summary>
        /// IId of the user being reported.
        /// </summary>
        public required string ReporterUserId { get; init; }

        /// <summary>
        /// Total count of reports against the user.
        /// </summary>
        public required string Message { get; init; }

        /// <summary>
        /// Metadata associated with the bug report.
        /// </summary>
        public required JToken Metadata { get; init; }

        /// <summary>
        /// Total count of distinct users who reported the user.
        /// </summary>

        /// <summary>
        /// The date time the user was last reported on.
        /// </summary>
        public required DateTime ReportedOn { get; init; }

        /// <summary>
        /// The files attached to the bug reports..
        /// </summary>
        public required IEnumerable<BugReportAttachmentSummary> Attachments { get; init; }
    }

    /// <summary>
    /// Represents an file attached to a bug report.
    /// </summary>
    public class BugReportAttachmentSummary
    {
        /// <summary>
        /// Url used to download the attachment content.
        /// </summary>
        public required string ContentType { get; init; }

        /// <summary>
        /// The content type of the attachment.
        /// </summary>
        public required string AttachmentId { get; init; }
    }
}
