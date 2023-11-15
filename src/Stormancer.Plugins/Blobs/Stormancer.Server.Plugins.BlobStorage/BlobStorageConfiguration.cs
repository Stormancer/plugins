using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Configuration section of the blob storage system.
    /// </summary>
    public class BlobStorageConfigurationSection
    {
        /// <summary>
        /// Gets the path of the blob store 
        /// </summary>
        public const string PATH = "storage";

        /// <summary>
        /// Gets the configurations of blob stores
        /// </summary>
        public Dictionary<string, JObject> BlobStores { get; set; } = new Dictionary<string, JObject>();
    }

    
}
