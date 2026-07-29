using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CommunityToolkit.HighPerformance;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Cluster.Application;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Aws.Config;
using Stormancer.Server.Plugins.BlobStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Aws
{
    internal class S3BlobStorageBackend : IBlobStorageBackend
    {
        private readonly ConfigCache _cache;
        private readonly ILogger _logger;

        public S3BlobStorageBackend(ConfigCache cache, ILogger logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public bool CanHandle(JObject backendConfiguration)
        {
            var config = backendConfiguration.ToObject<S3BlobStorageConfig>();
            return config?.Validate() ?? false;
        }

        private async Task<IAmazonS3> GetS3ClientAsync(S3BlobStorageConfig config)
        {
            var credentials = await _cache.GetS3Credentials(config);
            var region = RegionEndpoint.GetBySystemName(config.Region!);
            if (credentials != null)
            {
                return new AmazonS3Client(credentials, region);
            }
            else
            {
                // If no credentials are provided, use the default AWS SDK credential provider chain
                return new AmazonS3Client(region);
            }
        }

        public async ValueTask<CreateBlobResult> CreateBlobAsync(JObject configuration, string path, ReadOnlyMemory<byte> content, string contentType)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new CreateBlobResult { Success = false };
            }

            using var client = await GetS3ClientAsync(config);
            try
            {
                using var ms = content.AsStream();
                var putRequest = new PutObjectRequest
                {
                    BucketName = config.Bucket!,
                    Key = path,
                    InputStream = ms,
                    ContentType = contentType
                };
                await client.PutObjectAsync(putRequest);
                return new CreateBlobResult { Success = true, Path = path };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error creating blob '{path}' in bucket '{config.Bucket}'.", ex);
                return new CreateBlobResult { Success = false };
            }
        }

        private static string StagingPrefix(string path) => $"{path}.blocks/";

        public async ValueTask<StageBlockResult> StageBlobBlockAsync(JObject configuration, string path, string blobBlockId, ReadOnlyMemory<byte> content)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new StageBlockResult { Success = false };
            }

            using var client = await GetS3ClientAsync(config);
            var stagingKey = StagingPrefix(path) + blobBlockId;
            try
            {
                using var ms = content.AsStream();
                var req = new PutObjectRequest
                {
                    BucketName = config.Bucket!,
                    Key = stagingKey,
                    InputStream = ms
                };
                await client.PutObjectAsync(req);
                return new StageBlockResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error staging block '{blobBlockId}' for blob '{path}' in bucket '{config.Bucket}'.", ex);
                return new StageBlockResult { Success = false };
            }
        }

        public async ValueTask<CommitBlockListResult> CommitBlockListAsync(JObject configuration, string path, IEnumerable<string> blobBlockList)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new CommitBlockListResult { Success = false };
            }

            using var client = await GetS3ClientAsync(config);
            var bucket = config.Bucket!;
            // We'll concatenate staged parts into a temp file and upload as final object.
            string tempFile = Path.Combine(Path.GetTempPath(), $"s3-commit-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var outFs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    foreach (var blockId in blobBlockList)
                    {
                        var stagingKey = StagingPrefix(path) + blockId;
                        try
                        {
                            var getReq = new GetObjectRequest { BucketName = bucket, Key = stagingKey };
                            using var getResp = await client.GetObjectAsync(getReq);
                            await getResp.ResponseStream.CopyToAsync(outFs);
                        }
                        catch (AmazonS3Exception aex) when (aex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.Log(LogLevel.Warn, "blobStorage", $"Staged block not found: {stagingKey} when committing '{path}'", new { stagingKey, path });
                            return new CommitBlockListResult { Success = false, Reason = "missingBlock" };
                        }
                    }
                }

                // Upload final object
                using (var inFs = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var putReq = new PutObjectRequest
                    {
                        BucketName = bucket,
                        Key = path,
                        InputStream = inFs
                    };
                    await client.PutObjectAsync(putReq);
                }

                // Remove staged parts
                foreach (var blockId in blobBlockList)
                {
                    var stagingKey = StagingPrefix(path) + blockId;
                    try
                    {
                        var delReq = new DeleteObjectRequest { BucketName = bucket, Key = stagingKey };
                        await client.DeleteObjectAsync(delReq);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Warn, "blobStorage", $"Failed to delete staged block {stagingKey}: {ex.Message}", ex);
                    }
                }

                return new CommitBlockListResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error committing block list for '{path}' in bucket '{bucket}'.", ex);
                return new CommitBlockListResult { Success = false };
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        public async ValueTask<GetBlockListResult> GetBlobBlockListAsync(JObject configuration, string path)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new GetBlockListResult { Success = false };
            }

            using var client = await GetS3ClientAsync(config);
            var bucket = config.Bucket!;
            var prefix = StagingPrefix(path);

            try
            {
                var listReq = new ListObjectsV2Request { BucketName = bucket, Prefix = prefix };
                var result = await client.ListObjectsV2Async(listReq);
                var blocks = result.S3Objects.Select(o =>
                {
                    var id = o.Key.Substring(prefix.Length);
                    return new BlobBlock { Id = id, Size = o.Size.GetValueOrDefault() };
                });
                return new GetBlockListResult { Success = true, BlockList = blocks };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error listing staged blocks for '{path}' in bucket '{bucket}'.", ex);
                return new GetBlockListResult { Success = false };
            }
        }

        public async ValueTask<DeleteBlobResult> DeleteAsync(JObject configuration, string path)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new DeleteBlobResult { Success = false };
            }

            using var client = await GetS3ClientAsync(config);
            var bucket = config.Bucket!;

            try
            {
                var delReq = new DeleteObjectRequest { BucketName = bucket, Key = path };
                await client.DeleteObjectAsync(delReq);

                // Also remove staged objects under prefix
                var prefix = StagingPrefix(path);
                var listReq = new ListObjectsV2Request { BucketName = bucket, Prefix = prefix };
                var listResp = await client.ListObjectsV2Async(listReq);
                foreach (var obj in listResp.S3Objects)
                {
                    var del = new DeleteObjectRequest { BucketName = bucket, Key = obj.Key };
                    await client.DeleteObjectAsync(del);
                }

                return new DeleteBlobResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error deleting blob '{path}' in bucket '{bucket}'.", ex);
                return new DeleteBlobResult { Success = false };
            }
        }

        public async ValueTask<GetBlobContentResult> GetContentAsync(JObject configuration, string path)
        {
            var config = configuration.ToObject<S3BlobStorageConfig>();
            if (!(config?.Validate() ?? false))
            {
                return new GetBlobContentResult { Success = false, Reason = "invalidBackendConfig" };
            }

            using var client = await GetS3ClientAsync(config);
            var bucket = config.Bucket!;
            try
            {
                var getReq = new GetObjectRequest { BucketName = bucket, Key = path };
                var resp = await client.GetObjectAsync(getReq);
                // Return the response stream. Caller must dispose stream when done.
                return new GetBlobContentResult { Success = true, Content = resp.ResponseStream, ContentType = resp.Headers.ContentType };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new GetBlobContentResult { Success = false, Reason = "notFound" };
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "blobStorage", $"Error getting blob content for '{path}' in bucket '{bucket}'.", ex);
                return new GetBlobContentResult { Success = false, Reason = ex.Message };
            }
        }
    }
}
