using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Xsolla
{
    /// <summary>
    /// Configuration of the Xsolla plugin.
    /// </summary>
    public class XsollaConfigurationSection : IConfigurationSection<XsollaConfigurationSection>
    {
        ///<inheritdoc/>
        public static string SectionPath => "xsolla";

        ///<inheritdoc/>
        public static XsollaConfigurationSection Default => new XsollaConfigurationSection();


        /// <summary>
        /// Gets avalue indicating whether the Xsolla plugin is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Xsolla project id.
        /// </summary>
        public string? ProjectId { get; set; }


        /// <summary>
        /// XSolla OAuth Client id
        /// </summary>
        /// <remarks>
        /// See https://developers.xsolla.com/api/login/getting-user-token#getting-server-token
        /// </remarks>
        public string? ClientId { get; set; }

        /// <summary>
        /// Path to the secret storing the XSolla OAuth client secret.
        /// </summary>
        public string? ClientSecretPath { get; set; }

        /// <summary>
        /// Names of the Xsolla groups for which we allow linking multiple stormancer accounts
        /// </summary>
        public List<string> AllowMultipleLinkingFor { get; set; } = ["developer"];
    }
}
