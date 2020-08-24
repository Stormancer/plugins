using Stormancer.Server.Plugins.SpatialOS.Models;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SpatialOS
{
    /// <summary>
    /// A service to manage a SpatialOS application from a Stormancer application
    /// </summary>
    public interface ISpatialOSCredentialsService
    {
        /// <summary>
        /// Creates SpatialOs credentials for a user to connect to a deployment
        /// </summary>
        /// <param name="userId">The user id used in the generated credentials</param>
        /// <param name="providerName">The provider name used in the generated credentials</param>
        /// <param name="deploymentName">The deployment name used in the generated credentials</param>
        /// <param name="workerType">The worker type used in the generated credentials</param>
        /// <returns>SpatialOS credentials to use to connect a player to a SpatialOS deployment</returns>
        /// <remarks>The SpatialOS project name and service key used to generate the credentials are provided by the application's configuration.</remarks>
        Task<SpatialOsPlayerCredentials?> CreateSpatialOSToken(string userId, string providerName, string deploymentName, string workerType);
    }
}
