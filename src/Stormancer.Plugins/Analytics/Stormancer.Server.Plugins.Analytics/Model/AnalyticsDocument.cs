using Newtonsoft.Json.Linq;
using System;

namespace Stormancer.Server.Plugins.Analytics
{
    public class AnalyticsDocument
    {
        public string Index { get; set; }
        public string Type { get; set; }
        public DateTime CreationDate { get; set; }
        public JObject Content { get; set; }
        public string DeploymentId { get; set; }
        public string Category { get; set; }
        public string AccountId { get; set; }
        public string App { get; set; }
        public string Cluster { get; set; }
    }
}
