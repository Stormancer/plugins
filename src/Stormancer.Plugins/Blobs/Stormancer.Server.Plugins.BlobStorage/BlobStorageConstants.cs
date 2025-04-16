using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Provides constants related to blob storage.
    /// </summary>
    public static class BlobStorageConstants
    {
        /// <summary>
        /// Id of the scene containing the blob storage services.
        /// </summary>
        public const string SCENE_ID = "blobStorage";

        /// <summary>
        /// Metadata key for the blob storage service.
        /// </summary>
        public const string METADATA_KEY = "stormancer.blobStorage";
    }
}
