using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// A binary object attached to the bug report.
    /// </summary>
    public class BugReportAttachmentContent
    {
        /// <summary>
        /// Creates a new <see cref="BugReportAttachmentContent"/> object.
        /// </summary>
        /// <param name="contentType"></param>
        /// <param name="content"></param>
        /// <param name="owner"></param>
        public BugReportAttachmentContent(string contentType,string name, ReadOnlyMemory<byte> content)
        {
            ContentType = contentType;
            Name = name;
            Content = content;
          
        }

        /// <summary>
        /// Gets the content type of the attachment.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets the name of the attachment.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the binary data of the attachment.
        /// </summary>
        public ReadOnlyMemory<byte> Content { get; }

    }

    /// <summary>
    /// A bug report attachment.
    /// </summary>
    public class BugReportAttachment
    {
        /// <summary>
        /// Gets or sets the name of the attachment.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Gets or sets the content type of the attachment.
        /// </summary>
        public string ContentType { get; set; } = default!;

        /// <summary>
        /// Gets or sets the url used to download the attachment.
        /// </summary>
        public Uri Url { get; set; } = default!;
    }

    /// <summary>
    /// A bug report submitted by a client.
    /// </summary>
    public class BugReport
    {
        /// <summary>
        /// Gets the user id of the reporter.
        /// </summary>
        public string ReporterUserId { get; set; } = default!;

        /// <summary>
        /// Gets the message associated with the report.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Gets custom data associated with the report.
        /// </summary>
        public JObject CustomData { get; set; } = default!;

        /// <summary>
        /// Gets the list of blobs attached to the report.
        /// </summary>
        public IEnumerable<BugReportAttachment> Attachments { get; set; } = default!;

    }

    /// <summary>
    /// Bug report storage backend.
    /// </summary>
    public interface IBugReportingBackend
    {
        /// <summary>
        /// Processes a bug report.
        /// </summary>
        /// <param name="reporterId"></param>
        /// <param name="message"></param>
        /// <param name="customData"></param>
        /// <param name="attachments"></param>
        /// <remarks>Should only process the bug report if enabled in the configuration.</remarks>
        /// <returns></returns>
        Task ProcessBugReportAsync(string reporterId, string message, JObject customData, IEnumerable<BugReportAttachmentContent> attachments);

      
    }
}
