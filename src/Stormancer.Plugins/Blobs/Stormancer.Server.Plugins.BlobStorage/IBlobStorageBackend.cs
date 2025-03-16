using System.Threading.Tasks;
using System;
using Newtonsoft.Json.Linq;

namespace Stormancer.Server.Plugins.BlobStorage
{

    /// <summary>
    /// contract of a blob storage backend.
    /// </summary>
    public interface IBlobStorageBackend
    {
        /// <summary>
        /// Can the backend handles the provided config.
        /// </summary>
        /// <param name="backendConfiguration"></param>
        /// <returns></returns>
        bool CanHandle(JObject backendConfiguration);

        /// <summary>
        /// Creates a blob in the blob storage system.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        ValueTask<CreateBlobResult> CreateBlobAsync(JObject configuration, string path, ReadOnlyMemory<byte> content, string contentType);

        /// <summary>
        /// Deletes a blob
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<DeleteBlobResult> DeleteAsync(JObject configuration, string path);

        /// <summary>
        /// Gets the content of a blob.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<GetBlobContentResult> GetContentAsync(JObject configuration, string path);
    }

    
}