using Jose;
using MessagePack;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Context passed to <see cref="IBlobStorageEventHandler.OnCommittingBlocks(OnCommittingBlocksContext)"/>
    /// </summary>
    public class OnCommittingBlocksContext
    {
        /// <summary>
        /// Gets or sets the error reason, if <see cref="IsValid"/> is false.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// If false, cancel the operation, and returns the error in <see cref="Error"/>
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets the content of the upload token used by the client to perform the operation.
        /// </summary>
        public required UploadTokenPayload UploadToken { get; init; }

        /// <summary>
        /// Gets the list of committed blocks.
        /// </summary>
        public required IEnumerable<string> BlockList { get; init; }

        /// <summary>
        /// Gets the peer performing the operation.
        /// </summary>
        public required SessionId Peer { get; init; }


    }

    /// <summary>
    /// Context passed to <see cref="IBlobStorageEventHandler.OnCommittedBlocks(OnCommittedBlocksContext)"/>
    /// </summary>
    public class OnCommittedBlocksContext
    {
        /// <summary>
        /// Gets the content of the upload token used by the client to perform the operation.
        /// </summary>
        public required UploadTokenPayload UploadToken { get; init; }

        /// <summary>
        /// Gets the peer performing the operation.
        /// </summary>
        public required SessionId Peer { get; init; }

        /// <summary>
        /// Gets the list of committed blocks.
        /// </summary>
        public required IEnumerable<string> BlockList { get; init; }

        /// <summary>
        /// Gets the result of the commit blocks operation.
        /// </summary>
        public required CommitBlockListResult Result { get; init; }
    }

    /// <summary>
    /// Context passed to <see cref="IBlobStorageEventHandler.OnStagingBlock(OnStagingBlockContext)"/>
    /// </summary>
    public class OnStagingBlockContext
    {
        /// <summary>
        /// Gets or sets the error reason, if <see cref="IsValid"/> is false.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// If false, cancel the operation, and returns the error in <see cref="Error"/>
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Gets the content of the upload token used by the client to perform the operation.
        /// </summary>
        public required UploadTokenPayload UploadToken { get; init; }

        /// <summary>
        /// Gets the content of the block being staged.
        /// </summary>
        public required ReadOnlyMemory<byte> Content { get; init; }

        /// <summary>
        /// Gets the peer performing the operation.
        /// </summary>
        public required SessionId Peer { get; init; }
    }

    /// <summary>
    /// Context passed to <see cref="IBlobStorageEventHandler.OnStagedBlock(OnStagedBlockContext)"/>
    /// </summary>
    public class OnStagedBlockContext
    {
        /// <summary>
        /// Gets the content of the upload token used by the client to perform the operation.
        /// </summary>
        public required UploadTokenPayload UploadToken { get; init; }

        /// <summary>
        /// Gets the content of the block being staged.
        /// </summary>
        public required ReadOnlyMemory<byte> Content { get; init; }

        /// <summary>
        /// Gets the peer performing the operation.
        /// </summary>
        public required SessionId Peer { get; init; }

        /// <summary>
        /// Gets the result of the stage block operation.
        /// </summary>
        public required StageBlockResult Result { get; init; }
    }

    /// <summary>
    /// Provides a contract to react to blob upload events.
    /// </summary>
    public interface IBlobStorageEventHandler
    {

        /// <summary>
        /// Event fired before a block is staged.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        ValueTask OnStagingBlock(OnStagingBlockContext ctx);

        /// <summary>
        /// Event fired when a block has been staged.
        /// </summary>
        /// <returns></returns>
        ValueTask OnStagedBlock(OnStagedBlockContext ctx);

        /// <summary>
        /// Event fired when 
        /// </summary>
        /// <returns></returns>
        ValueTask OnCommittingBlocks(OnCommittingBlocksContext ctx);
        /// <summary>
        /// Event fired when the block list of a blob is updated.
        /// </summary>
        /// <returns></returns>
        ValueTask OnCommittedBlocks(OnCommittedBlocksContext ctx);
    }

    /// <summary>
    /// Payload of an upload token.
    /// </summary>
    public class UploadTokenPayload
    {
        /// <summary>
        /// Gets the Id of the blob store containing the blob represented by the token.
        /// </summary>
        public required string BlobStoreId { get; init; }

        /// <summary>
        /// Gets the path of the blob represented by the token.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// Gets the metadata associated with the token.
        /// </summary
        public required Dictionary<string, string> Metadata { get; init; }
    }
    /// <summary>
    /// Interface of the blob storage system.
    /// </summary>
    public interface IBlobStorage
    {

        /// <summary>
        /// Creates a token used to upload a blob in a subsequent call.
        /// </summary>
        /// <param name="blobStoreId"></param>
        /// <param name="path"></param>
        /// <param name="metadata"></param>
        /// <param name="maxBlobSize"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<string> CreateBlobUploadTokenAsync(string blobStoreId, string path, Dictionary<string, string> metadata, ulong maxBlobSize = 32 * 1024 * 1024, CancellationToken cancellationToken = default);


        /// <summary>
        /// Decodes an upload token.
        /// </summary>
        /// <param name="token"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<UploadTokenPayload> DecodeBlobUploadTokenAsync(string token, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a blob in the blob storage system.
        /// </summary>
        /// <param name="blobStoreId"></param>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        ValueTask<CreateBlobResult> CreateBlobAsync(string blobStoreId, string path, ReadOnlyMemory<byte> content, string contentType);

        /// <summary>
        /// Deletes a blob
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<DeleteBlobResult> DeleteAsync(string path);

        /// <summary>
        /// Gets the content of a blob.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<GetBlobContentResult> GetBlobContent(string path);

        /// <summary>
        /// Stages a blob block in the storage system.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="blobUploadToken"></param>
        /// <param name="blockId"></param>
        /// <param name="content"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<StageBlockResult> StageBlockAsync(SessionId origin, string blobUploadToken, string blockId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

        /// <summary>
        /// Commits a list of blob blocks present in the blob staging area.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="blobUploadToken"></param>
        /// <param name="blockIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<CommitBlockListResult> CommitBlockListAsync(SessionId origin, string blobUploadToken, IEnumerable<string> blockIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the list of blocks in a block blob.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        ValueTask<GetBlockListResult> GetBlockListAsync(string path);

    }

    /// <summary>
    /// Result of a create blob request.
    /// </summary>
    public class CreateBlobResult
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        [MemberNotNullWhen(true, "Path")]
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

    /// <summary>
    /// Result of a Stage block operation.
    /// </summary>
    [MessagePackObject]
    public class StageBlockResult
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        [Key(0)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets a string representing the reason why the operation failed.
        /// </summary>
        [Key(1)]
        public string? Reason { get; init; }
    }

    /// <summary>
    /// Result of a Stage block operation.
    /// </summary>
    [MessagePackObject]
    public class CommitBlockListResult
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        [Key(0)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets a string representing the reason why the operation failed.
        /// </summary>
        [Key(1)]
        public string? Reason { get; init; }
    }

    /// <summary>
    /// A  blob block.
    /// </summary>

    public class BlobBlock
    {
        /// <summary>
        /// Gets the id of the block blob.
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets the block size.
        /// </summary>
        public required long Size { get; init; }
    }
    /// <summary>
    /// Result of a <see cref="IBlobStorage.GetBlockListAsync"/> operation.
    /// </summary>
    public class GetBlockListResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        [MemberNotNullWhen(true, "BlockList")]
        public bool Success { get; init; }


        /// <summary>
        /// Gets a <see cref="string"/> providing a reason for the failure.
        /// </summary>
        public string? Reason { get; init; }


        /// <summary>
        /// List of blocks in the blob.
        /// </summary>
        public IEnumerable<BlobBlock>? BlockList { get; init; }

        /// <summary>
        /// Gets the length of the blob
        /// </summary>
        public int BlobContentLength { get; init; }
    }
    /// <summary>
    /// Result of a get blob request.
    /// </summary>
    public class GetBlobContentResult : IDisposable
    {
        /// <summary>
        /// Gets or sets a value indicating if the operation was successful.
        /// </summary>
        [MemberNotNullWhen(true, "Content")]
        [MemberNotNullWhen(true, "ContentType")]
        [MemberNotNullWhen(false, "Reason")]
        public required bool Success { get; init; }

        /// <summary>
        /// Gets a <see cref="System.IO.Stream"/> object exposing the blob content.
        /// </summary>
        public Stream? Content { get; init; }

        /// <summary>
        /// Gets a <see cref="string"/> providing a reason for the failure.
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Gets the content type of the blob.
        /// </summary>
        public string? ContentType { get; init; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Content?.Dispose();
        }
    }

    internal class BlobStorageKeyStore
    {
        private readonly object _keyLock = new();
        private byte[]? Key { get; set; }

        public byte[] GetKey()
        {
            if (Key == null)
            {
                lock (_keyLock)
                {
                    if (Key == null)
                    {
                        Key = new byte[32];
                        using var generator = RandomNumberGenerator.Create();

                        generator.GetBytes(Key);
                    }
                }
            }

            return Key;
        }
    }
    internal class BlobStorage : IBlobStorage
    {
        private readonly IEnumerable<IBlobStorageBackend> _backends;
        private readonly Lazy<IEnumerable<IBlobStorageEventHandler>> _eventHandlers;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly BlobStorageKeyStore _keyStore;

        public BlobStorage(
            IEnumerable<IBlobStorageBackend> backends,
            Lazy<IEnumerable<IBlobStorageEventHandler>> eventHandlers,
            IConfiguration configuration,
            ILogger logger,
            BlobStorageKeyStore keyStore)
        {
            _backends = backends;
            _eventHandlers = eventHandlers;
            _configuration = configuration;
            _logger = logger;
            _keyStore = keyStore;
        }
        public async ValueTask<CreateBlobResult> CreateBlobAsync(string blobStoreId, string path, ReadOnlyMemory<byte> content, string contentType)
        {
            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            if (!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return new CreateBlobResult { Success = false };
            }

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    var result = await backend.CreateBlobAsync(storeConfiguration, path, content, contentType);
                    if (result.Success)
                    {
                        result.Path = CreatePath(blobStoreId, result.Path);
                        return result;
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

            if (firstSlash > -1)
            {
                return (path.Substring(0, firstSlash), path.Substring(firstSlash + 1));
            }
            else
            {
                throw new ArgumentException($"'{path}' is not a valid blob path", nameof(path));
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

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    return backend.DeleteAsync(storeConfiguration, blobPath);
                }
            }

            return ValueTask.FromResult(new DeleteBlobResult { Success = false });

        }

        public ValueTask<GetBlobContentResult> GetBlobContent(string path)
        {
            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            var (blobStoreId, blobPath) = ParsePath(path);

            if (!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return ValueTask.FromResult(new GetBlobContentResult { Success = false, Reason = $"blobStoreNotFound?id={blobStoreId}" });
            }

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    return backend.GetContentAsync(storeConfiguration, blobPath);
                }
            }

            return ValueTask.FromResult(new GetBlobContentResult { Success = false, Reason = $"blobStoreBackendNotFound?id={blobStoreId}" });
        }

        public ValueTask<string> CreateBlobUploadTokenAsync(string blobStoreId, string path, Dictionary<string, string> metadata, ulong maxBlobSize = 33554432, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(JWT.Encode(new UploadTokenPayload { BlobStoreId = blobStoreId, Path = path, Metadata = metadata }, _keyStore.GetKey(), JwsAlgorithm.HS256));
        }

        public ValueTask<UploadTokenPayload> DecodeBlobUploadTokenAsync(string token, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(JWT.Decode<UploadTokenPayload>(token, _keyStore.GetKey(), JwsAlgorithm.HS256));
        }


        public async ValueTask<StageBlockResult> StageBlockAsync(SessionId origin, string blobUploadToken, string blockId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
        {
            var tokenPayload = await DecodeBlobUploadTokenAsync(blobUploadToken, cancellationToken);
            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            if (!config.BlobStores.TryGetValue(tokenPayload.BlobStoreId, out var storeConfiguration))
            {
                return new StageBlockResult { Success = false, Reason = $"blobStoreNotFound?id={tokenPayload.BlobStoreId}" };
            }

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    var onStagingBlockContext = new OnStagingBlockContext { Content = content, Peer = origin, UploadToken = tokenPayload };
                    if (!onStagingBlockContext.IsValid)
                    {
                        return new StageBlockResult { Success = false, Reason = onStagingBlockContext.Error };
                    }
                    await _eventHandlers.Value.RunEventHandler(async h => await h.OnStagingBlock(onStagingBlockContext), ex => _logger.Log(LogLevel.Error, "blobStorage", "An error occurred while executing IBlobStorageEventHandler.OnStagingBlock", ex));
                    var result = await backend.StageBlobBlockAsync(storeConfiguration, tokenPayload.Path, blockId, content);

                    var onStagedBlockContext = new OnStagedBlockContext { Result = result, Content = content, UploadToken = tokenPayload, Peer = origin };
                    await _eventHandlers.Value.RunEventHandler(async h => await h.OnStagedBlock(onStagedBlockContext), ex => _logger.Log(LogLevel.Error, "blobStorage", "An error occurred while executing IBlobStorageEventHandler.OnStagedBlock", ex));

                    return result;
                }
            }

            return new StageBlockResult { Success = false, Reason = $"blobStoreBackendNotFound?id={tokenPayload.BlobStoreId}" };
        }

        public async ValueTask<CommitBlockListResult> CommitBlockListAsync(SessionId origin, string blobUploadToken, IEnumerable<string> blockIds, CancellationToken cancellationToken)
        {

            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();
            var tokenPayload = await DecodeBlobUploadTokenAsync(blobUploadToken, cancellationToken);
            var blobStoreId = tokenPayload.BlobStoreId;
            var path = tokenPayload.Path;

            if (!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return new CommitBlockListResult { Success = false, Reason = $"blobStoreNotFound?id={blobStoreId}" };
            }

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    var onCommittingBlockContext = new OnCommittingBlocksContext { BlockList = blockIds, Peer = origin, UploadToken = tokenPayload };
                    await _eventHandlers.Value.RunEventHandler(async h => await h.OnCommittingBlocks(onCommittingBlockContext), ex => _logger.Log(LogLevel.Error, "blobStorage", "An error occurred while executing IBlobStorageEventHandler.OnCommittingBlocks", ex));
                    if (!onCommittingBlockContext.IsValid)
                    {
                        return new CommitBlockListResult { Success = false, Reason = onCommittingBlockContext.Error };
                    }

                    var result = await backend.CommitBlockListAsync(storeConfiguration, path, blockIds);

                    var onCommittedBlockContext = new OnCommittedBlocksContext { Result = result, BlockList = blockIds, Peer = origin, UploadToken = tokenPayload };
                    await _eventHandlers.Value.RunEventHandler(async h => await h.OnCommittedBlocks(onCommittedBlockContext), ex => _logger.Log(LogLevel.Error, "blobStorage", "An error occurred while executing IBlobStorageEventHandler.OnCommittedBlocks", ex));

                    return result;
                }
            }

            return new CommitBlockListResult { Success = false, Reason = $"blobStoreBackendNotFound?id={blobStoreId}" };
        }

        public ValueTask<GetBlockListResult> GetBlockListAsync(string path)
        {

            var config = _configuration.GetValue<BlobStorageConfigurationSection>(BlobStorageConfigurationSection.PATH) ?? new BlobStorageConfigurationSection();

            var (blobStoreId, blobPath) = ParsePath(path);

            if (!config.BlobStores.TryGetValue(blobStoreId, out var storeConfiguration))
            {
                return ValueTask.FromResult(new GetBlockListResult { Success = false, Reason = $"blobStoreNotFound?id={blobStoreId}" });
            }

            foreach (var backend in _backends)
            {
                if (backend.CanHandle(storeConfiguration))
                {
                    return backend.GetBlobBlockListAsync(storeConfiguration, blobPath);
                }
            }

            return ValueTask.FromResult(new GetBlockListResult { Success = false, Reason = $"blobStoreBackendNotFound?id={blobStoreId}" });
        }


    }


}
