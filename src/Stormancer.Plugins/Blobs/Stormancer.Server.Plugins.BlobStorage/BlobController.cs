
using MessagePack;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{

    /// <summary>
    /// Provides API to upload block blobs.
    /// </summary>
    [Service(Named=false,ServiceType = BlobStorageConstants.METADATA_KEY)]
    public class BlobController : ControllerBase
    {
        private readonly IBlobStorage _blobStorage;

        /// <summary>
        /// Creates a new <see cref="BlobController"/> object.
        /// </summary>
        /// <param name="blobStorage"></param>
        public BlobController(IBlobStorage blobStorage)
        {
            _blobStorage = blobStorage;
        }

        /// <summary>
        /// Stages a block into a blob staging area.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<StageBlockResult> StageBlock(StageBlockArgs args, RequestContext<IScenePeerClient> request)
        {
            int length = (int)(request.InputStream.Length - request.InputStream.Position);
            using var  owner = MemoryPool<byte>.Shared.Rent(length);
            var buffer = owner.Memory.Slice(0, length);
            request.InputStream.Read(buffer.Span);
            return await _blobStorage.StageBlockAsync(request.RemotePeer.SessionId,args.Token, args.BlockId, buffer,request.CancellationToken);
        }

        /// <summary>
        /// Commits a blob from block ids.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<CommitBlockListResult> CommitBlocks(CommitBlobArgs args,RequestContext<IScenePeerClient> request)
        {
            return await _blobStorage.CommitBlockListAsync(request.RemotePeer.SessionId,args.Token, args.BlockIds,request.CancellationToken);
        }

        [S2SApi]
        public async Task<string> CreateUploadToken(string blobStoreId, string path, Dictionary<string, string> metadata, ulong maxBlobSize)
        {
            return await _blobStorage.CreateBlobUploadTokenAsync(blobStoreId, path, metadata, maxBlobSize);
        }
    }

    /// <summary>
    /// Arguments for <see cref="BlobController.StageBlock(StageBlockArgs, RequestContext{IScenePeerClient})"/>
    /// </summary>
    [MessagePackObject]
    public class StageBlockArgs
    {
        /// <summary>
        /// Gets or sets the token to use to authorize upload.
        /// </summary>
        [Key(0)]
        public required string Token { get; init; }

        /// <summary>
        /// Gets or sets the id of the block to upload.
        /// </summary>
        [Key(1)]
        public required string BlockId { get; init; }
    }

    /// <summary>
    /// Arguments for <see cref="BlobController.CommitBlocks(CommitBlobArgs, RequestContext{IScenePeerClient})"/>
    /// </summary>
    [MessagePackObject]
    public class CommitBlobArgs
    {
        /// <summary>
        /// Gets or sets the token to use to authorize upload.
        /// </summary>
        [Key(0)]
        public required string Token { get; init; }

        /// <summary>
        /// Gets the list of successive block id used to commit the blob.
        /// </summary>
        [Key(1)]
        public required IEnumerable<string> BlockIds { get; init; }
        
    }
}
