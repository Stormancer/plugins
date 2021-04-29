using Newtonsoft.Json.Linq;
using System;

namespace Stormancer.Server.Plugins.Analytics
{
    /// <summary>
    /// An analytics document.
    /// </summary>
    public class AnalyticsDocument
    {
        /// <summary>
        /// Gets or sets the index storing the document.
        /// </summary>
        public string Index { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of document.
        /// </summary>
        public string Type { get; set; } = default!;

        /// <summary>
        /// Gets or sets the date the document was created.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the json data of the document.
        /// </summary>
        public JObject Content { get; set; } = default!;

        /// <summary>
        /// Gets or sets the deployment id that produced the document.
        /// </summary>
        public string DeploymentId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the category of the document.
        /// </summary>
        public string Category { get; set; } = default!;

        /// <summary>
        /// Gets or sets the account id of the app that produced the document.
        /// </summary>
        public string AccountId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the id of the app that produced the document.
        /// </summary>
        public string App { get; set; } = default!;

        /// <summary>
        /// Gets or sets the cluster that produced the document.
        /// </summary>
        public string Cluster { get; set; } = default!;
    }
}
