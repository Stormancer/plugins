using Stormancer.Core;
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
            var config = host.DependencyResolver.Resolve<PartyConfigurationService>();
          
            PartyConfigurationBuilder builder = new(config);

            builder = builderFct(builder);
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
        /// Default authorized invitation code character list.
        /// </summary>
        public const string DEFAULT_AUTHORIZED_INVITATION_CODE_CHARACTERS = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        
        /// <summary>
        /// Default invitation code length;
        /// </summary>
        public const int DEFAULT_INVITATION_CODE_LENGTH = 6;


        /// <summary>
        /// List of characters authorized to create invitation codes
        /// </summary>
        public string? authorizedInvitationCodeCharacters { get; set; } 

        /// <summary>
        /// Length of invitation codes.
        /// </summary>
        public int? invitationCodeLength { get; set; }

        /// <summary>
        /// Enables platform invitation.
        /// </summary>
        public bool? EnablePlatformInvitation { get; set; }

    }


    internal class PartyConfigurationService
    {
        private readonly IConfiguration configuration;

        public PartyConfigurationService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        private PartyConfigurationSection GetConfigSection()
        {
            return configuration.GetValue<PartyConfigurationSection>("party") ?? new PartyConfigurationSection();
        }
        internal string GetAuthorizedInvitationCodeCharacters(ISceneHost partyScene)
        {
            var configValue = GetConfigSection().authorizedInvitationCodeCharacters;
            if(configValue !=null)
            {
                return configValue;
            }
            var characters = GetAuthorizedInvitationCodeCharactersFunc?.Invoke(partyScene);
            if (characters is null)
            {
                return PartyConfigurationSection.DEFAULT_AUTHORIZED_INVITATION_CODE_CHARACTERS;
            }
            else
            {
                return characters;
            }
        }

        internal int GetInvitationCodeLength(ISceneHost partyScene)
        {
            var configValue = GetConfigSection().invitationCodeLength;
            if(configValue!=null)
            {
                return configValue.Value;
            }
            var length = GetInvitationLengthFunc?.Invoke(partyScene);
            if (length is null)
            {
                return PartyConfigurationSection.DEFAULT_INVITATION_CODE_LENGTH;
            }
            else
            {
                return length.Value;
            }
        }
        internal void OnPartyCreating(PartyCreationContext ctx)
        {
            var enablePlatformInvitation = GetConfigSection().EnablePlatformInvitation;
            if(enablePlatformInvitation !=null)
            {
                ctx.PartyRequest.ServerSettings.ShouldCreatePlatformLobby(true);
            }

            if(ShouldEnablePlatformLobbiesFunc is not null)
            {
                ShouldEnablePlatformLobbiesFunc(ctx);
            }
        }
        internal Func<ISceneHost, string?>? GetAuthorizedInvitationCodeCharactersFunc { get; set; }
        internal Func<ISceneHost, int?>? GetInvitationLengthFunc { get; set; }

        internal Action<PartyCreationContext>? ShouldEnablePlatformLobbiesFunc { get; set; }
    }

    /// <summary>
    /// Provides functions to modify the configuration of the party plugin.
    /// </summary>
    public class PartyConfigurationBuilder
    {
        /// <summary>
        /// Party configuration section
        /// </summary>
        internal PartyConfigurationService Configuration { get; }

        internal PartyConfigurationBuilder(PartyConfigurationService section)
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
            Configuration.GetAuthorizedInvitationCodeCharactersFunc = _ => characters;
            return this;
        }

        /// <summary>
        /// Sets the length of invitation codes.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public PartyConfigurationBuilder InvitationCodeLength(int length)
        {
            Configuration.GetInvitationLengthFunc = _ => length;
            return this;
        }

        /// <summary>
        /// Enables platform invitation in all parties.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PartyConfigurationBuilder EnablePlatformInvitations(bool value = true)
        {
            Configuration.ShouldEnablePlatformLobbiesFunc = ctx => ctx.PartyRequest.ServerSettings.ShouldCreatePlatformLobby(value);
            return this;
        }
    }
}
