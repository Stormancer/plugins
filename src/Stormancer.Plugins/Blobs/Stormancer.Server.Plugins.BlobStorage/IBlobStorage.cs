using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Interface of the blob storage system.
    /// </summary>
    public interface IBlobStorage
    {
        /// <summary>
        /// Creates a blob in the blob storage system.
        /// </summary>
        /// <param name="blobStoreId"></param>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        ValueTask<CreateBlobResult> CreateBlobAsync(string blobStoreId,string path, ReadOnlyMemory<byte> content, string contentType);

        /// <summary>
        /// Deletes a blob
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<DeleteBlobResult> DeleteAsync(string path);
    }

    /// <summary>
    /// Result of a create blob request.
    /// </summary>
    public class CreateBlobResult
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        [MemberNotNullWhen(true,"Path")]
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the path of the created blob in the storage system.
        /// </summary>
        public string? Path { get; set; }
    }

    /// <summary>
    /// Result of a delete blob request.
    /// </summary>
    public class DeleteBlobResult
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        public bool Success { get; set; }
    }

    internal class BlobStorage : IBlobStorage
    {
        private readonly IEnumerable<IBlobStorageBackend> _backends;
        private readonly IConfiguration _configuration;

        public BlobStorage(IEnumerable<IBlobStorageBackend> backends, IConfiguration configuration )
        {
            _backends = backends;
            _configuration = configuration;
        }
        public async ValueTask<CreateBlobResult> CreateBlobAsync(string blobStoreId, string path, ReadOnlyMemory<byte> content, string contentType)
        {
            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            if(!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return new CreateBlobResult { Success = false };
            }

            foreach(var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    var result = await backend.CreateBlobAsync(storeConfiguration, path, content, contentType);
                    if (result.Success)
                    {
                        result.Path = CreatePath(blobStoreId, result.Path);
                    }
                    else
                    {
                        return result;
                    }
                }
            }

            return new CreateBlobResult { Success = false };
        }

        private (string blobStoreId, string path) ParsePath(string path)
        {
            var firstSlash = path.IndexOf('/');

            if(firstSlash > -1)
            {
                return (path.Substring(0,firstSlash), path.Substring(firstSlash + 1));
            }
            else
            {
                throw new ArgumentException($"'{path}' is not a valid blob path",nameof(path));
            }
            
        }

        private string CreatePath(string blobStoreId, string path)
        {
            return $"{blobStoreId}/{path}";
        }

        public ValueTask<DeleteBlobResult> DeleteAsync(string path)
        {
            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            var (blobStoreId, blobPath) = ParsePath(path);

            if (!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return ValueTask.FromResult(new DeleteBlobResult { Success = false });
            }

            foreach(var backend in _backends)
            {
                if(backend.CanHandle(storeConfiguration))
                {
                    return backend.DeleteAsync(storeConfiguration,blobPath);
                }
            }

            return ValueTask.FromResult(new DeleteBlobResult { Success = false });

        }
    }


}
