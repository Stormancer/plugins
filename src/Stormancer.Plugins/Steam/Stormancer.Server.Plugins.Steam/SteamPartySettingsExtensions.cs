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
        /// Get create a Steam lobby with the party.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <returns>Boolena value indicating the party will create a steam lobby.</returns>
        public static bool? SteamCreateLobby(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.CreateLobby, out var steamCreateLobby) ? bool.Parse(steamCreateLobby) : settings.CreatePlatformLobby();
        }

        /// <summary>
        /// Create a Steam lobby with the party.
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <param name="create">A boolean value indicating whether a Steam lobby should be created.</param>
        /// <returns>Settings.</returns>
        public static ServerPartySettings SteamCreateLobby(this ServerPartySettings settings, bool? create)
        {
            if (create != null)
            {
                settings[SteamSettingsConstants.CreateLobby] = create.ToString()!;
            }
            else
            {
                settings.Remove(SteamSettingsConstants.CreateLobby);
            }
            return settings;
        }

        /// <summary>
        /// Gets the max number of members in the steam lobby.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <returns>Max members count to use on lobby creation.</returns>
        public static int? SteamMaxMembers(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.MaxMembers, out var steamMaxMembers) ? int.Parse(steamMaxMembers) : settings.MaxMembers();
        }

        /// <summary>
        /// Sets the max number of members in the Steam lobby.
        /// </summary>
        /// <remarks>
        /// This value overrides the max number of members set globally using the MaxMembers(int?) method for Steam lobbies.
        /// If not set, the global value is used. If no global value was set, the default value is 5.
        /// </remarks>
        /// <param name="settings">Settings.</param>
        /// <param name="maxMembers">Steam lobby max members count.</param>
        /// <returns>Settings.</returns>
        public static ServerPartySettings SteamMaxMembers(this ServerPartySettings settings, int? maxMembers)
        {
            if (maxMembers != null)
            {
                settings[SteamSettingsConstants.MaxMembers] = maxMembers.ToString()!;
            }
            else
            {
                settings.Remove(SteamSettingsConstants.MaxMembers);
            }
            return settings;
        }

        /// <summary>
        /// Gets current steam lobby type for the party.
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <returns>Lobby type to use on lobby creation.</returns>
        public static LobbyType? SteamLobbyType(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.LobbyType, out var v) ? (LobbyType?)Enum.Parse<LobbyType>(v, true) : null;
        }

        /// <summary>
        /// Sets the steam lobby type.
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <param name="lobbyType"></param>
        /// <returns>Settings.</returns>
        public static ServerPartySettings SteamLobbyType(this ServerPartySettings settings, LobbyType? lobbyType)
        {
            if (lobbyType != null)
            {
                settings[SteamSettingsConstants.LobbyType] = lobbyType.ToString()!;
            }
            else
            {
                settings.Remove(SteamSettingsConstants.LobbyType);
            }
            return settings;
        }
    }
}
