using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// A binary object attached to the bug report.
    /// </summary>
    public class BugReportAttachment
    {
        public BugReportAttachment(string contentType, ReadOnlyMemory<byte> content)
        {
            ContentType = contentType;
            Content = content;
        }

        /// <summary>
        /// Gets or sets the content type of the attachment.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets the binary data of the attachment.
        /// </summary>
        public ReadOnlyMemory<byte> Content { get; set; }
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
        public Task ProcessBugReport(string reporterId, string message, JObject customData, IEnumerable<BugReportAttachment> attachments);

      
    }
}
