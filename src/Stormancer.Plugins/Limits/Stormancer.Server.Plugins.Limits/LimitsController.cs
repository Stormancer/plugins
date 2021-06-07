using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Provides S2S API for limits
    /// </summary>
    public class LimitsController : ControllerBase
    {
        private readonly ILimits limits;

        /// <summary>
        /// Creates a controller instance.
        /// </summary>
        /// <param name="limits"></param>
        public LimitsController(ILimits limits)
        {
            this.limits = limits;
        }

        /// <summary>
        /// Gets data about connection limits.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public UserConnectionLimitStatus GetConnectionLimitStatus()
        {
            return limits.GetUserLimitsStatus();
        }


    }

    /// <summary>
    /// Cache for <see cref="UserConnectionLimitStatus"/> instances retrieved from S2S.
    /// </summary>
    public class LimitsClientCache
    {
        /// <summary>
        /// Cache
        /// </summary>
        public MemoryCache<UserConnectionLimitStatus> Value { get; } = new MemoryCache<UserConnectionLimitStatus>();
    }
    /// <summary>
    /// Client to get limits informations.
    /// </summary>
    public class LimitsClient
    {
        private readonly RpcService rpc;
        private readonly IServiceLocator locator;
        private readonly ISerializer serializer;
        private readonly LimitsClientCache cache;

        /// <summary>
        /// Create a new instance of the class.
        /// </summary>
        /// <param name="rpc"></param>
        /// <param name="locator"></param>
        /// <param name="serializer"></param>
        /// <param name="cache"></param>
        public LimitsClient(RpcService rpc, IServiceLocator locator, ISerializer serializer, LimitsClientCache cache)
        {
            this.rpc = rpc;
            this.locator = locator;
            this.serializer = serializer;
            this.cache = cache;
        }

        /// <summary>
        /// Gets connection limits state.
        /// </summary>
        /// <returns></returns>
        public Task<UserConnectionLimitStatus> GetConnectionLimitStatus()
        {
            return cache.Value.Get("limits", _ =>
            {
                return Retries.Retry<UserConnectionLimitStatus>(async (_) =>
               {
                   var sceneId = await locator.GetSceneId("stormancer.authenticator", string.Empty);
                   return await rpc.Rpc("Limits.GetConnectionLimitStatus", new MatchSceneFilter(sceneId), s => { }, PacketPriority.MEDIUM_PRIORITY).Select(p =>
                   {
                       using (p)
                       {
                           return serializer.Deserialize<UserConnectionLimitStatus>(p.Stream);
                       }
                   }).SingleOrDefaultAsync();
               }, RetryPolicies.ConstantDelay(4, TimeSpan.FromMilliseconds(500)), default, ex => true);
            }, TimeSpan.FromSeconds(1));

        }
    }
}
