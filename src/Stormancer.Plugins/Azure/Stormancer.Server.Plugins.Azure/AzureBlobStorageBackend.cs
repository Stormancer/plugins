using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.BlobStorage;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Azure
{
    internal class ConfigCache
    {
        public ConfigCache(ISecretsStore secretStore)
        {
            _secretStore = secretStore;
        }

        public DateTime _lastUpdated;

        private string? _currentPath;
        private Task<string?>? _connectionStringTask;
        private readonly ISecretsStore _secretStore;
        private object _syncRoot = new object();
        public Task<string?> GetConnectionString(string? path)
        {
            lock (_syncRoot)
            {
                if (_connectionStringTask == null || _currentPath != path || _lastUpdated < DateTime.UtcNow - TimeSpan.FromMinutes(1))
                {
                    _currentPath = path;
                    _lastUpdated = DateTime.UtcNow;
                    _connectionStringTask = GetConnectionStringImpl();
                }

                return _connectionStringTask;

            }
        }

        private async Task<string?> GetConnectionStringImpl()
        {
            if (_currentPath != null)
            {
                var secret = await _secretStore.GetSecret(_currentPath);
                return secret.Value != null ? Encoding.UTF8.GetString(secret.Value) : null;
            }
            else
            {
                return null;
            }

        }
    }

    /// <summary>
    /// Configuration of a blob storage account.
    /// </summary>
    public class AzureBlobStorageConfig
    {
        /// <summary>
        /// Gets or sets the type of the configuration.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the path to the connection string in the secret store.
        /// </summary>
        public string? ConnectionStringPath { get; set; }


        /// <summary>
        /// Gets or sets the container to use.
        /// </summary>
        public string? Container { get; set; }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns></returns>
        [MemberNotNullWhen(true, "Container", "ConnectionStringPath")]
        public bool Validate()
        {
            return Type == "azureBlob" && ConnectionStringPath != null && Container != null;
        }
    }

    internal class AzureBlobStorageBackend : IBlobStorageBackend
    {
        private readonly ConfigCache _cache;
        private readonly ILogger _logger;

        public AzureBlobStorageBackend(ConfigCache cache, ILogger logger)
        {
            _cache = cache;
            _logger = logger;
        }


        public bool CanHandle(JObject backendConfiguration)
        {
            var config = backendConfiguration.ToObject<AzureBlobStorageConfig>();
            return config?.Validate() ?? false;
        }

        public async ValueTask<CreateBlobResult> CreateBlobAsync(JObject configuration, string path, ReadOnlyMemory<byte> content, string contentType)
        {
            var config = configuration.ToObject<AzureBlobStorageConfig>();

            if (!(config?.Validate() ?? false))
            {
                return new CreateBlobResult { Success = false };
            }

            var connectionString = await _cache.GetConnectionString(config.ConnectionStringPath);
            var client = new BlobServiceClient(connectionString);

            var blobContainerClient = client.GetBlobContainerClient(config.Container);
            try
            {
                var response = await blobContainerClient.UploadBlobAsync(path, new BinaryData(content));

                return new CreateBlobResult { Success = true, Path = path };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"An error occurred while creating the blob '{path}' in {client.AccountName}/{config.Container}.", ex);
                return new CreateBlobResult { Success = false };
            }
        }

        public async ValueTask<DeleteBlobResult> DeleteAsync(JObject configuration, string path)
        {
            var config = configuration.ToObject<AzureBlobStorageConfig>();

            if (!(config?.Validate() ?? false))
            {
                return new DeleteBlobResult { Success = false };
            }

            var connectionString = await _cache.GetConnectionString(config.ConnectionStringPath);
            var client = new BlobServiceClient(connectionString);

            var blobContainerClient = client.GetBlobContainerClient(config.Container);

            var response = await blobContainerClient.DeleteBlobIfExistsAsync(path);

            return new DeleteBlobResult { Success = response.Value };

        }

        public async ValueTask<GetBlobContentResult> GetContentAsync(JObject configuration, string path)
        {
            var config = configuration.ToObject<AzureBlobStorageConfig>();

            if (!(config?.Validate() ?? false))
            {
                return new GetBlobContentResult { Success = false, Reason = "invalidBackendConfig" };
            }

            try
            {
                var connectionString = await _cache.GetConnectionString(config.ConnectionStringPath);
                var client = new BlobServiceClient(connectionString);

                var blobContainerClient = client.GetBlobContainerClient(config.Container);

                var blobClient = blobContainerClient.GetBlobClient(path);
                var result = await blobClient.DownloadStreamingAsync();
                return new GetBlobContentResult { Success = true, Content = result.Value.Content, ContentType = result.Value.Details.ContentType };
            }
            catch (RequestFailedException ex)
            {
                return new GetBlobContentResult { Success = false, Reason = ex.Message };

            }
        }
    }
}
