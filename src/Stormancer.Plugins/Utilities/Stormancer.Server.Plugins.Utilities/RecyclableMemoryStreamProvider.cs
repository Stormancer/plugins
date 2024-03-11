using Microsoft.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Utilities
{
    public class RecyclableMemoryStreamProvider
    {
        private RecyclableMemoryStreamManager _manager = new RecyclableMemoryStreamManager();

        public RecyclableMemoryStream GetStream()
        {
            return _manager.GetStream();
        }
    }
}
