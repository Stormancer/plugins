using Stormancer.Server.Plugins.Steam;
using System;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Party settings extension methods.
    /// </summary>
    public static class SteamPartySettingsExtensions
    {
        /// <summary>
        /// Create a Steam lobby with the party.
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <param name="create">A boolean value indicating whether a Steam lobby should be created.</param>
        /// <returns></returns>
        public static ServerPartySettings CreateSteamLobby(this ServerPartySettings settings, bool create)
        {
            settings[SteamSettingsConstants.CreateLobbyPartyServerSetting] = create.ToString();

            return settings;
        }

        /// <summary>
        /// Sets the steam lobby type.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="lobbyType"></param>
        /// <returns></returns>
        public static ServerPartySettings SteamLobbyType(this ServerPartySettings settings, LobbyType lobbyType)
        {
            settings[SteamSettingsConstants.CreateLobbyPartyServerSetting] = lobbyType.ToString();
            return settings;
        }

        /// <summary>
        /// Gets current steam lobby type for the party.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static LobbyType SteamLobbyType(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.LobbyTypePartyServerSetting, out var v) ? Enum.Parse<LobbyType>(v, true) : LobbyType.FriendsOnly;
        }

        /// <summary>
        /// Gets the max number of members in the steam lobby.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static int SteamLobbyMaxMembers(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.MaxMembersPartyServerSettings, out var steamMaxMembers) ? int.Parse(steamMaxMembers) : settings.MaxMembers() ?? 5;
        }

        /// <summary>
        /// Sets the max number of members in the Steam lobby.
        /// </summary>
        /// <remarks>
        /// This value overrides the max number of members set globally using the MaxMembers(int?) method for Steam lobbies.
        /// If not set, the global value is used. If no global value was set, the default value is 5.
        /// </remarks>
        /// <param name="settings"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ServerPartySettings SteamLobbyMaxMembers(this ServerPartySettings settings ,int? value)
        {
            if (value != null)
            {
                settings[SteamSettingsConstants.MaxMembersPartyServerSettings] = value.ToString()!;
            }
            else
            {
                settings.Remove(SteamSettingsConstants.MaxMembersPartyServerSettings);
            }
            return settings;
        }
    }
}
