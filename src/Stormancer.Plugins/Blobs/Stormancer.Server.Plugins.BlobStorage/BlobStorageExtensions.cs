using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Extension methods for 
    /// </summary>
    public static class BlobStorageExtensions
    {
        /// <summary>
        /// Adds blob storage features to the scene.
        /// </summary>
        /// <param name="scene"></param>
        public static void AddBlobStorage(this ISceneHost scene)
        {
            scene.TemplateMetadata[BlobStorageConstants.METADATA_KEY] = "1.0.0";
        }
    }
}
