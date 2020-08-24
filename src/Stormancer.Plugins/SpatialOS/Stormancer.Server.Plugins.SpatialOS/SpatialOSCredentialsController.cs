using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.SpatialOS.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.Users;
using Microsoft.Extensions.Logging;

namespace Stormancer.Server.Plugins.SpatialOS
{
    /// <summary>
    /// A controller that allows a scene to generate SpatialOS credentials
    /// </summary>
    public class SpatialOSCredentialsController : ControllerBase
    {
        private readonly ISpatialOSCredentialsService _spatialOSCredentialsService;
        private readonly IUserSessions _sessions;
        private readonly IEnumerable<ISpatialOsCredentialsEventHandler> _handlers;
        private readonly ILogger _logger;
        private readonly SpatialOSConfiguration _configuration;

        /// <summary>
        /// Constructor used by the infrastructure. Do not call directly.
        /// </summary>
        /// <param name="spatialOSCredentialsService"></param>
        /// <param name="settings"></param>
        /// <param name="sessions"></param>
        /// <param name="handlers"></param>
        /// <param name="logger"></param>
        public SpatialOSCredentialsController(ISpatialOSCredentialsService spatialOSCredentialsService,
            IConfiguration settings,
            IUserSessions sessions,
            IEnumerable<ISpatialOsCredentialsEventHandler> handlers,
            ILogger logger)
        {
            _spatialOSCredentialsService = spatialOSCredentialsService;
            _sessions = sessions;
            _handlers = handlers;
            _logger = logger;

            var config = ((JObject)settings.Settings.spatialos).ToObject<SpatialOSConfiguration>();
            if (config == null)
            {
                throw new InvalidOperationException("spatialos config should not be null.");
            }
            _configuration = config;
        }

        /// <summary>
        /// A route that allows a connected client to get SpatialOS credentials
        /// </summary>
        /// <param name="arguments">Custom arguments consumed by the event handlers.</param>
        /// <returns>The SpatialOS player credentials</returns>
        /// <remarks>
        /// The behaviour of this route can be modified by implementing <see cref="ISpatialOsCredentialsEventHandler"/>.
        /// </remarks>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<SpatialOsPlayerCredentials> GetCredentials(JObject arguments)
        {
            var session = await _sessions.GetSession(Request.RemotePeer);


            var context = new SpatialOsPlayerCredentialsCtx(session, arguments, _configuration.DefaultDeploymentName);
            await _handlers.RunEventHandler(handler => handler.OnCreatingSpatialOsCredentials(context), ex =>
            {
                _logger.LogError(ex, "An error occurred when running event handlers before creating SpatialOS credentials.");
                throw new InvalidOperationException("An error occurred when running event handlers before creating SpatialOS credentials, see inner exception for details.", ex);
            });

            if(context.UserId == null)
            {
                throw new InvalidOperationException("Connot determine user id for creating SpatialOS credentials.");
            }
            if(context.ProviderName == null)
            {
                throw new InvalidOperationException("Connot determine provider name for creating SpatialOS credentials.");
            }
            if (context.DeploymentName == null)
            {
                throw new InvalidOperationException($"Cannot determine deployment name for creating SpatialOS credentials. Set it in the application's settings or have a {nameof(ISpatialOsCredentialsEventHandler)} set it.");
            }

            var result = await _spatialOSCredentialsService.CreateSpatialOSToken(context.UserId, context.ProviderName, context.DeploymentName, context.WorkerType);

            if(result == null)
            {
                throw new ClientException($"Deployment {context.DeploymentName} was not found in SpatialOS.");
            }

            return result;
        }
    }
}
