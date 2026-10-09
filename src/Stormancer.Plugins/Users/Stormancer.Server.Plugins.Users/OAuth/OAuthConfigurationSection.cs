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
        public static string SectionPath => "auth.oauth";

        /// <inheritdoc/>
        public static OAuthConfigurationSection Default { get; } = new OAuthConfigurationSection();


        /// <summary>
        /// Gets the secret store path storing the keys for the OAuth service.
        /// </summary>
        /// <remarks>
        /// The file this path points is a json document deserialized into a <see cref="KeySets"/> object:
        ///``` 
        /// {
        ///    "keys":[
        ///         {jwk}
        ///         ....
        ///       }
        ///    }
        /// }
        /// ```
        /// </remarks>
        public string? KeysPath { get; set; }

        /// <summary>
        /// The Key id the app must use to sign JSON Web Tokens.
        /// </summary>
        public string? ActiveKeyId { get; set; }

        /// <summary>
        /// The issuer to use
        /// </summary>
        public string? Issuer { get; set; }

        /// <summary>
        /// List of accepted audiences.
        /// </summary>
        public IEnumerable<string> ValidAudiences { get; set; } = Enumerable.Empty<string>();
    }
}
