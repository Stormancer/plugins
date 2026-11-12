using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.OAuth
{
    /// <summary>
    /// Configuration section for OAuth related features.
    /// </summary>
    public class OAuthConfigurationSection : IConfigurationSection<OAuthConfigurationSection>
    {
        /// <inheritdoc/>
        public static string SectionPath => "oauth";

        /// <inheritdoc/>
        public static OAuthConfigurationSection Default { get; } = new OAuthConfigurationSection();


        /// <summary>
        /// Gets the secret store path storing the keys for the OAuth service.
        /// </summary>
        /// <remarks>
        /// The file this path points is a json document deserialized into a <see cref="KeysSet"/> object:
        ///``` 
        /// {
        ///    "keys":[
        ///         {
        ///             "createdOn":{dateTime}
        ///             "key":{jwk}
        ///       
        ///       },
        ///         ....
        ///    }
        /// }
        /// ```
        /// </remarks>
        public string? KeysPath { get; set; }

        /// <summary>
        /// Time period a key should be kept in the keys set.
        /// </summary>
        /// <remarks>
        /// Expressed in the format `d.hh:mm:ss`, defaults to `3.00:00:00`
        /// </remarks>
        public TimeSpan KeysRetentionPeriod { get; set; } = TimeSpan.FromDays(3);

        /// <summary>
        /// The time period after which the system generates a new key automatically.
        /// </summary>
        /// <remarks>
        /// Expressed in the format `d.hh:mm:ss`, defaults to `1.00:00:00`
        /// </remarks>
        public TimeSpan KeyRenewalPeriod { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// The issuer to use
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// List of accepted audiences.
        /// </summary>
        public IEnumerable<string> ValidAudiences { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Key size in bits to use when generating new keys.
        /// </summary>
        /// <remarks>
        /// Defaults to 2048.
        /// </remarks>
        public int KeySize { get; set; } = 2048;
    }
}
