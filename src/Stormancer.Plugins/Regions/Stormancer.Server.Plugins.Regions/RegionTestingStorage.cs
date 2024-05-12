using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{
    /// <summary>
    /// Cache of available regions and how to test them.
    /// </summary>
    public class RegionTestingStorage : IDisposable
    {
        /// <summary>
        /// Cached region data
        /// </summary>
        public MemoryCache<int, Dictionary<string, string>> Cache { get; } = new MemoryCache<int, Dictionary<string, string>>();

        ///<inheritdoc/>
        public void Dispose()
        {
           Cache.Dispose();
        }
    }
}
