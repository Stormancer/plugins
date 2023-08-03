
using Stormancer.Server.Plugins.API;
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
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task UnlockItem(string itemId, CancellationToken cancellationToken)
        {

        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task GetItemDefinitions(CancellationToken cancellationToken)
        {

        }

        [S2SApi]
        public async Task GetItems(string userId)
        {

        }
    }
}