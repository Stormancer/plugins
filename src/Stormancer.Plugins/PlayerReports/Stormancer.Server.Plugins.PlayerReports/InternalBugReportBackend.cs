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
using System.Text.RegularExpressions;
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
                    var attachmentId = GenerateSlug(attachment.Name);
                    var result = await _blobStorage.CreateBlobAsync(config.AttachmentStorageId, $"bugReports/{report.Id}/{attachmentId}", attachment.Content, attachment.ContentType);
                    if (result.Success)
                    {
                        storedAttachments.Add(new BugReportAttachementRecord { Name = attachment.Name, ContentType = attachment.ContentType, AttachmentId = attachmentId, AttachmentPath = result.Path, StorageId = config.AttachmentStorageId });
                    }
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


        private static string GenerateSlug(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Convert to lowercase
            input = input.ToLowerInvariant();

            // Remove diacritics (accents) from Latin characters
            input = RemoveDiacritics(input);

            // Replace spaces and invalid characters with hyphens
            input = Regex.Replace(input, @"[^a-z0-9\s-]", ""); // Allow only alphanumeric, spaces, and hyphens
            input = Regex.Replace(input, @"\s+", "-").Trim();  // Replace spaces with hyphens
            input = Regex.Replace(input, @"-+", "-");         // Replace multiple hyphens with a single one

            return input;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
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
