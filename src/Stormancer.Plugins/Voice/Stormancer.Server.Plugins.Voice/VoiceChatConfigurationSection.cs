using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Voice
{
    /// <summary>
    /// Voice chat configuration section.
    /// </summary>
    public class VoiceChatConfigurationSection : IConfigurationSection<VoiceChatConfigurationSection>
    {
        ///<inheritdoc/>
        public static string SectionPath => "voice";

        ///<inheritdoc/>
        public static VoiceChatConfigurationSection Default { get; } = new VoiceChatConfigurationSection();

        /// <summary>
        /// The type of voice provider to use.
        /// </summary>
        public string? ProviderType { get; set; }
    }
}
