namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Party settings constants.
    /// </summary>
    public static class SettingsConstants
    {
        /// <summary>
        /// Create a platform lobby
        /// </summary>
        public const string ShouldCreatePlatformLobby = "party.shouldCreatePlatformLobby";

        /// <summary>
        /// Max member party settings key.
        /// </summary>
        public const string MaxMembers = "party.maxMembers";
    }

    /// <summary>
    /// Extension methods for party settings
    /// </summary>
    public static class PartySettingsExtensions
    {
        /// <summary>
        /// Gets the platform lobby creation.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static bool? ShouldCreatePlatformLobby(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SettingsConstants.ShouldCreatePlatformLobby, out var createPlatformLobby) ? (bool?)bool.Parse(createPlatformLobby) : null;
        }

        /// <summary>
        /// Sets the platform lobby creation.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="createPlatformLobby"></param>
        /// <returns></returns>
        public static ServerPartySettings ShouldCreatePlatformLobby(this ServerPartySettings settings, bool? createPlatformLobby)
        {
            if (createPlatformLobby != null)
            {
                settings[SettingsConstants.ShouldCreatePlatformLobby] = createPlatformLobby.ToString()!;
            }
            else
            {
                settings.Remove(SettingsConstants.ShouldCreatePlatformLobby);
            }
            return settings;
        }

        /// <summary>
        /// Gets the current maximum number of members in the party.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static int? MaxMembers(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SettingsConstants.MaxMembers, out var maxMembers) ? (int?)int.Parse(maxMembers) : null;
        }

        /// <summary>
        /// Sets the maximum number of members in the party.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="maxMembers"></param>
        /// <returns></returns>
        public static ServerPartySettings MaxMembers(this ServerPartySettings settings, int? maxMembers)
        {
            if (maxMembers != null)
            {
                settings[SettingsConstants.MaxMembers] = maxMembers.ToString()!;
            }
            else
            {
                settings.Remove(SettingsConstants.MaxMembers);
            }
            return settings;
        }
    }
}
