using Microsoft.EntityFrameworkCore;
using Nest;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.BlobStorage;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    internal class InternalBugReportBackend : IBugReportingBackend
    {
        private readonly IBlobStorage _blobStorage;
        private readonly DbContextAccessor _dbContextAccessor;
        private readonly ConfigurationMonitor<BugReportsInternalBackendConfigurationSection> _configuration;

        public InternalBugReportBackend(IBlobStorage blobStorage, DbContextAccessor dbContextAccessor, ConfigurationMonitor<BugReportsInternalBackendConfigurationSection> configuration)
        {
            _blobStorage = blobStorage;
            _dbContextAccessor = dbContextAccessor;
            _configuration = configuration;
        }

        public string Type => "internal";

        public async Task ProcessBugReportAsync(string reporterId, string message, JObject customData, IEnumerable<BugReportAttachmentContent> attachments, CancellationToken cancellationToken)
        {
            var storedAttachments = new List<BugReportAttachementRecord>();
            try
            {
                var config = _configuration.Value;
                if (config.AttachmentStorageId == null)
                {
                    return;
                }

                var dbContext = await _dbContextAccessor.GetDbContextAsync();

                var guid = Guid.Parse(reporterId);

                var report = new BugReportRecord
                {
                    Id = Guid.NewGuid(),
                    Message = message,
                    CreatedOn = DateTime.UtcNow,
                    ReporterId = guid,
                    Context = JsonDocument.Parse(customData.ToString())
                };


                foreach (var attachment in attachments)
                {
                    var result = await _blobStorage.CreateBlobAsync(config.AttachmentStorageId, $"bugReports/{report.Id}/{attachment.Name}", attachment.Content, attachment.ContentType);
                    storedAttachments.Add(new BugReportAttachementRecord { Name = attachment.Name, ContentType = attachment.ContentType, AttachmentPath = result.Path });
                }

                report.Attachements = JsonSerializer.SerializeToDocument(storedAttachments);


                await dbContext.Set<BugReportRecord>().AddAsync(report);
            }
            catch
            {
                foreach (var attachment in storedAttachments)
                {
                    await _blobStorage.DeleteAsync(attachment.AttachmentPath);
                }
                throw;
            }
        }
    }

    /// <summary>
    /// Configuration of the bug reports internal storage backend.
    /// </summary>
    public class BugReportsInternalBackendConfigurationSection : IConfigurationSection<BugReportsInternalBackendConfigurationSection>
    {

        /// <inheritdoc/>
        public static string SectionPath { get; } = "bugReports.backends.internal";

        /// <inheritdoc/>
        public static BugReportsInternalBackendConfigurationSection Default { get; } = new BugReportsInternalBackendConfigurationSection();


        /// <summary>
        /// Id of the blob store used to store bug report attachments.
        /// </summary>
        public string? AttachmentStorageId { get; set; }
    }
}
