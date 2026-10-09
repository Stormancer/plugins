using Microsoft.Extensions.Configuration;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Epic configuration class
    /// </summary>
    public class EOSConfigurationSection : IConfigurationSection<EOSConfigurationSection>
    {
        ///<inheritdoc/>
        public static string SectionPath => "eos";

        ///<inheritdoc/>
        public static EOSConfigurationSection Default => new EOSConfigurationSection();

        /// <summary>
        /// Allowed Product ids.
        /// </summary>
        public IEnumerable<string>? productIds { get; set; }

        /// <summary>
        /// Allowed Application ids.
        /// </summary>
        public IEnumerable<string>? applicationIds { get; set; }

        /// <summary>
        /// Allowed Deployment ids.
        /// </summary>
        public string? deploymentId { get; set; }

        /// <summary>
        /// Allowed Sandbox ids.
        /// </summary>
        public IEnumerable<string>? sandboxIds { get; set; }

        /// <summary>
        /// Client id.
        /// </summary>
        public string? clientId { get; set; }

        /// <summary>
        /// Client secret.
        /// </summary>
        public string? clientSecret { get; set; }

        /// <summary>
        /// Audience used to generate Json web tokens if requested by the client.
        /// </summary>
        /// <remarks>
        /// The audience must be in the list of audiences accepted by the OAuth configuration (see <see cref="OAuthConfigurationSection"/>).
        /// </remarks>
        public string? audience { get; set; }
    }
}
