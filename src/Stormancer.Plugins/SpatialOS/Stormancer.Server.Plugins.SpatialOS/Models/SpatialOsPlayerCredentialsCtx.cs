using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.SpatialOS.Models
{
    /// <summary>
    /// Context used by handlers before creating SpatialOS player credentials
    /// </summary>
    public class SpatialOsPlayerCredentialsCtx
    {
        /// <summary>
        /// The user to create credentials for
        /// </summary>
        public User User => Session.User;

        /// <summary>
        /// The current session of the user to create credentials for
        /// </summary>
        public Session Session { get; }

        /// <summary>
        /// Custom data provided to the SpatialOS Credentials controller
        /// </summary>
        public JObject CustomData { get; }

        /// <summary>
        /// The user id used in the SpatialOS credentials
        /// </summary>
        /// <remarks>
        /// Defaults to User.Id
        /// Setting it null will make the credentials creation fail.
        /// </remarks>
        public string? UserId { get; set; }

        /// <summary>
        /// The SpatialOS deployment name to create credentials for.
        /// </summary>
        /// <remarks>
        /// Defaults to the property "spatialos.DefaultDeploymentName" in the Stormancer application settings.
        /// </remarks>
        public string? DeploymentName { get; set; }

        /// <summary>
        /// The provider name used in the SpatialOS credentials
        /// </summary>
        /// <remarks>
        /// defaults to "stormancer/{platform}", platform depending on the login platform used by the current Session
        /// </remarks>
        public string ProviderName { get; set; }

        /// <summary>
        /// The worker type used int the SpatialOS credentials
        /// </summary>
        /// <remarks>
        /// Defaults to "UnrealClient"
        /// </remarks>
        public string WorkerType { get; set; } = "UnrealClient";        

        internal SpatialOsPlayerCredentialsCtx(Session session, JObject customData, string? defaultDeploymentName)
        {            
            Session = session;
            CustomData = customData;
            UserId = User.Id;
            DeploymentName = defaultDeploymentName;
            ProviderName = $"stormancer/{session.platformId.Platform}";
        }
    }
}
