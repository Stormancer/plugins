using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{
    /// <summary>
    /// Results of a set of region latency tests.
    /// </summary>
    public class GetLatencyTestsResponse
    {
        /// <summary>
        /// Test results
        /// </summary>
        /// <remarks>
        /// [region]=>ping
        /// </remarks>
        public IEnumerable<LatencyTestResult> Results { get; set; } = Enumerable.Empty<LatencyTestResult>();
    }

    /// <summary>
    /// The result of a latency test.
    /// </summary>
    public class LatencyTestResult
    {
        /// <summary>
        /// Gets or sets the id of the tested region
        /// </summary>
        public string Region { get; set; } = default!;

        /// <summary>
        /// Gets or sets the latency associated with the tested region.
        /// </summary>
        public int Latency { get; set; }
    }

    public class LatencyTestRequest
    {
       
        /// <summary>
        /// List of Ips to test to choose a region.
        /// </summary>
        /// <remarks>
        /// [region]=>[ip]
        /// </remarks>
        public Dictionary<string, string> TestIps { get; set; }
    }

}
