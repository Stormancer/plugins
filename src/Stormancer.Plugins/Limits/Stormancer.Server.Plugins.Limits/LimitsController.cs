using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Provides S2S API for limits
    /// </summary>
    [Service(Named = false, ServiceType = "stormancer.plugins.limits")]
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
        [S2SApi]
        public UserConnectionLimitStatus GetConnectionLimitStatus()
        {
            return limits.GetUserLimitsStatus();
        }

        /// <summary>
        /// Called whenever a player disconnects from the scene.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            ((Limits)limits).RemoveFromQueue(args.Peer.SessionId);
            return Task.CompletedTask;
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
        private readonly LimitsProxy limitsProxy;

        /// <summary>
        /// Create a new instance of the class.
        /// </summary>
        /// <param name="rpc"></param>
        /// <param name="locator"></param>
        /// <param name="serializer"></param>
        /// <param name="cache"></param>
        /// <param name="limitsProxy"></param>
        public LimitsClient(RpcService rpc, IServiceLocator locator, ISerializer serializer, LimitsClientCache cache, LimitsProxy limitsProxy)
        {
            this.rpc = rpc;
            this.locator = locator;
            this.serializer = serializer;
            this.cache = cache;
            this.limitsProxy = limitsProxy;
        }

        /// <summary>
        /// Gets connection limits state.
        /// </summary>
        /// <returns></returns>
        public Task<UserConnectionLimitStatus> GetConnectionLimitStatus(CancellationToken cancellationToken)
        {
            return cache.Value.Get("limits", _ =>  Retries.Retry<UserConnectionLimitStatus>(
                    (_) => limitsProxy.GetConnectionLimitStatus(cancellationToken), 
                    RetryPolicies.ConstantDelay(4, TimeSpan.FromMilliseconds(500)), 
                    cancellationToken, 
                    ex => true)
            , TimeSpan.FromSeconds(1));

        }
    }
}
