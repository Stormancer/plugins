using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Linked account
    /// </summary>
    public class LinkedAccount
    {
        /// <summary>
        /// Identity provider Id
        /// </summary>
        [JsonPropertyName("identityProviderId")]
        public string IdentityProviderId { get; set; } = null!;

        /// <summary>
        /// Display name
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = null!;
    }

    /// <summary>
    /// Account
    /// </summary>
    public class Account
    {
        /// <summary>
        /// Account Id
        /// </summary>
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = null!;

        /// <summary>
        /// Display name
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// Preferred language
        /// </summary>
        [JsonPropertyName("preferredLanguage")]
        public string? PreferredLanguage { get; set; } = null!;

        /// <summary>
        /// Linked accounts
        /// </summary>
        [JsonPropertyName("linkedAccounts")]
        public IEnumerable<LinkedAccount>? LinkedAccounts { get; set; }
    }
}
