
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Stormancer.Server.Plugins.Collections
{
    /// <summary>
    /// Provides API to interact with collections.
    /// </summary>
    [Service(Named = false, ServiceType = CollectionsPlugin.SERVICE_ID)]
    public class CollectionsController : ControllerBase
    {
        private readonly ICollectionService _collectionService;
        private readonly IUserSessions _userSessions;

        public CollectionsController(ICollectionService collectionService, IUserSessions userSessions)
        {
            _collectionService = collectionService;
            _userSessions = userSessions;
        }
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UnlockItem(RequestContext<IScenePeerClient> ctx, string itemId)
        {
            var session = await _userSessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }
            await _collectionService.UnlockAsync(session.User, itemId, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string,CollectableItemDefinition>> GetItemDefinitions(RequestContext<IScenePeerClient> ctx)
        {
            var session = await _userSessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }

            return await _collectionService.GetItemDefinitionsAsync(ctx.CancellationToken);
        }

        [S2SApi]
        public async Task<IEnumerable<string>> GetItems(RequestContext<IScenePeerClient> ctx, string userId)
        {
            var session = await _userSessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }

            return (await _collectionService.GetCollectionAsync(new[] { userId }, ctx.CancellationToken))[userId];
        }
    }
}