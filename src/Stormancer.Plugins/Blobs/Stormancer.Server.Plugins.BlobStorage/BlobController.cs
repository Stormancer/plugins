using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{

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
        public async Task StageBlock(StageBlockArgs args, RequestContext<IScenePeerClient> request)
        {
            int length = (int)(request.InputStream.Length - request.InputStream.Position);
            using var  owner = MemoryPool<byte>.Shared.Rent(length);
            var buffer = owner.Memory.Slice(0, length);
            request.InputStream.Read(buffer.Span);
            await _blobStorage.StageBlockAsync(args.Token, args.BlockId, buffer,request.CancellationToken);
        }

        /// <summary>
        /// Commits a blob from block ids.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CommitBlob(CommitBlobArgs args,CancellationToken cancellationToken)
        {
            await _blobStorage.CommitBlockListAsync(args.Token, args.BlockIds,cancellationToken);
        }
    }

    /// <summary>
    /// Arguments for <see cref="BlobController.StageBlock(StageBlockArgs, RequestContext{IScenePeerClient})"/>
    /// </summary>
    public class StageBlockArgs
    {
        /// <summary>
        /// Gets or sets the token to use to authorize upload.
        /// </summary>
        public required string Token { get; init; }

        /// <summary>
        /// Gets or sets the id of the block to upload.
        /// </summary>
        public required string BlockId { get; init; }
    }

    /// <summary>
    /// Arguments for <see cref="BlobController.CommitBlob(CommitBlobArgs, CancellationToken)"/>
    /// </summary>
    public class CommitBlobArgs
    {
        /// <summary>
        /// Gets or sets the token to use to authorize upload.
        /// </summary>
        public required string Token { get; init; }

        /// <summary>
        /// Gets the list of successive block id used to commit the blob.
        /// </summary>
        public required IEnumerable<string> BlockIds { get; init; }
        
    }
}
