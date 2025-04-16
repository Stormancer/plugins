using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        /// <summary>
        /// Commits a list of block in the block staging area.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <param name="blobBlockList"></param>
        /// <returns></returns>
        ValueTask<CommitBlockListResult> CommitBlockListAsync(JObject configuration, string path, IEnumerable<string> blobBlockList);

        /// <summary>
        /// Stages a block in in a blob staging area.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <param name="blobBlockId"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        ValueTask<StageBlockResult> StageBlobBlockAsync(JObject configuration, string path, string blobBlockId, ReadOnlyMemory<byte> content);

        /// <summary>
        /// Gets the list of blocks in a blob.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<GetBlockListResult> GetBlobBlockListAsync(JObject configuration, string path);
    }

    
}