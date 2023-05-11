using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{
    public class RegionTestingStorage
    {
        public MemoryCache<int, Dictionary<string, string>> Cache { get; } = new MemoryCache<int, Dictionary<string, string>>();
    }
}
