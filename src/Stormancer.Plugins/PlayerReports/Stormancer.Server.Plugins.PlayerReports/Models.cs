
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// Represents a report by a player on another player.
    /// </summary>
    public class PlayerReport : IDisposable
    {
        /// <summary>
        /// Gets or sets the id of the report.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Message associated with the report.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Custom context data in json.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public JsonDocument Context { get; set; } = default!;

        /// <summary>
        /// Gets or sets the player reporting the other player.
        /// </summary>
        public UserRecord Reporter { get; set; } = default!;

        /// <summary>
        /// Gets or sets the player reported in the report.
        /// </summary>
        public UserRecord Reported { get; set; } = default!;

        /// <summary>
        /// Gets or sets the utc time this record was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        ///<inheritdoc/>
        public void Dispose()
        {
            Context?.Dispose();
        }
    }

    /// <summary>
    /// Record of a bug report in the database.
    /// </summary>
    public class BugReportRecord : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the Id of the reporter.
        /// </summary>
        public Guid ReporterId { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="UserRecord"/> who created the report.
        /// </summary>
        [ForeignKey("ReporterId")]
        public UserRecord Reporter { get; set; } = default!;

        /// <summary>
        /// Gets or sets the message associated with the bug report.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Custom json context associated with the bug report.
        /// </summary>
        public JsonDocument Context { get; set; } = default!;

        /// <summary>
        /// Gets or sets the date the bug record was created (UTC)
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the list of file attachments associated with the report.
        /// </summary>
        public JsonDocument Attachements { get; set; } = default!;

        ///<inheritdoc/>
        public void Dispose()
        {
            Context?.Dispose();
            Attachements?.Dispose();
        }
    }

    /// <summary>
    /// File attachment
    /// </summary>
    public class BugReportAttachementRecord
    {
        /// <summary>
        /// Name of the attachment.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the path of the attachment.
        /// </summary>
        public string AttachmentPath { get; set; } = default!;

        /// <summary>
        /// Gets or sets the content type of the attachment.
        /// </summary>
        public string ContentType { get; set; } = default!;

    }

    internal class ModelConfigurator : Stormancer.Server.Plugins.Database.EntityFrameworkCore.IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {
            modelBuilder.Entity<PlayerReport>();
            modelBuilder.Entity<BugReportRecord>();
        }
    }
}
