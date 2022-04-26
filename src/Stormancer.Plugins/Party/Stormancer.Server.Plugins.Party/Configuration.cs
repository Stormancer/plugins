using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Provides extension methods to configure parties.
    /// </summary>
    public static class PartyConfigurationExtensions
    {
        /// <summary>
        /// Configures player parties.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="builderFct"></param>
        /// <returns></returns>
        public static IHost ConfigurePlayerParty(this IHost host, Func<PartyConfigurationBuilder, PartyConfigurationBuilder> builderFct)
        {
            var config = host.DependencyResolver.Resolve<IConfiguration>();
            var section = config.GetValue<PartyConfigurationSection>("party") ?? new PartyConfigurationSection();

            PartyConfigurationBuilder builder = new(section);

            builder = builderFct(builder);

            config.SetDefaultValue("party", builder.Configuration);
            return host;
        }
    }

    /// <summary>
    /// Configuration section of the party plugin.
    /// </summary>
    /// <remarks>
    /// The section is saved in the configuration under the key "party".
    /// </remarks>
    public class PartyConfigurationSection
    {
        /// <summary>
        /// List of characters authorized to create invitation codes
        /// </summary>
        public string authorizedInvitationCodeCharacters { get; set; } = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        /// <summary>
        /// Length of invitation codes.
        /// </summary>
        public int invitationCodeLength { get; set; } = 6;

    }

    /// <summary>
    /// Provides functions to modify the configuration of the party plugin.
    /// </summary>
    public class PartyConfigurationBuilder
    {
        /// <summary>
        /// Party configuration section
        /// </summary>
        public PartyConfigurationSection Configuration { get; }

        internal PartyConfigurationBuilder(PartyConfigurationSection section)
        {
            Configuration = section;

        }

        /// <summary>
        /// Sets the characters used to generate invitation codes.
        /// </summary>
        /// <param name="characters"></param>
        /// <remarks>Defaults value is "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"</remarks>
        /// <returns></returns>
        public PartyConfigurationBuilder AuthorizedInvitationCodeCharacters(string characters)
        {
            Configuration.authorizedInvitationCodeCharacters = characters;
            return this;
        }

        /// <summary>
        /// Sets the length of invitation codes.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public PartyConfigurationBuilder InvitationCodeLength(int length)
        {
            Configuration.invitationCodeLength = length;
            return this;
        }
    }
}
