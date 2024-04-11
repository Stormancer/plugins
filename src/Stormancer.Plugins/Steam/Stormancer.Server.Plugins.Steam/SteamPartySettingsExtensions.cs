using Stormancer.Server.Plugins.Steam;
using System;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Steam settings constants.
    /// </summary>
    public static class SteamSettingsConstants
    {
        /// <summary>
        /// Setting key used to decide if a steam lobby should be created when a party is created.
        /// </summary>
        /// <remarks>
        /// Setting key: "steam.createLobby"
        /// Possible values: "true" or "false".
        /// The setting defaults to true.
        /// Must be set before party creation (ie in OnCreatingParty) or by the client.
        /// </remarks>
        /// <example>
        /// Task OnCreatingParty(PartyCreationContext ctx)
        /// {
        ///     ctx.PartyRequest.ServerSettings[Settings.CreateLobbyPartyServerSetting] = "false";
        /// }
        /// </example>
        public const string ShouldCreateLobby = "steam.shouldCreateLobby";

        /// <summary>
        /// Setting key used to set the type of lobby
        /// </summary>
        /// <remarks>
        /// Setting key: "steam.lobbyType"
        /// Possible values: enum LobbyType stringified (cf. Steam lobby documentation).
        /// The setting defaults to LobbyType.FriendsOnly.
        /// Must be set before party creation (ie in OnCreatingParty) or by the client.
        /// </remarks>
        /// <example>
        /// Task OnCreatingParty(PartyCreationContext ctx)
        /// {
        ///     ctx.PartyRequest.ServerSettings[Settings.LobbyTypePartyServerSetting] = LobbyType.FriendsOnly.ToString();
        /// }
        /// </example>
        public const string LobbyType = "steam.lobbyType";

        /// <summary>
        /// Setting key used to determine if the plugin should make steam lobbies non joinable when the party is made non joinable
        /// </summary>
        public const string SyncJoinable = "steam.joinableSync";

        /// <summary>
        /// Setting key used to set the max number of players in a Steam lobby
        /// </summary>
        /// <remarks>
        /// Setting key: "steam.maxMembers"
        /// Possible values: "1" to "255" (cf. Steam lobby documentation).
        /// The setting defaults to "5".
        /// Must be set before party creation (ie in OnCreatingParty) or by the client.
        /// </remarks>
        /// <example>
        /// Task OnCreatingParty(PartyCreationContext ctx)
        /// {
        ///     ctx.PartyRequest.ServerSettings[Settings.MaxMembersPartyServerSettings] = "5";
        /// }
        /// </example>
        public const string MaxMembers = "steam.maxMembers";

        /// <summary>
        /// Setting key used to set whether party join should fail if steam lobby creation failed.
        /// </summary>
        /// <remarks>
        /// "true" or "false". Defaults to false.
        /// </remarks>
        public const string DoNotJoinIfLobbyCreationFailed = "steam.doNotJoinIfLobbyCreationFailed";
    }

    /// <summary>
    /// Party settings extension methods.
    /// </summary>
    public static class SteamPartySettingsExtensions
    {
        /// <summary>
        /// Get create a Steam lobby with the party.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <returns>Boolean value indicating the party will create a steam lobby.</returns>
        public static bool? ShouldCreateSteamLobby(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.ShouldCreateLobby, out var steamCreateLobby) ? bool.Parse(steamCreateLobby) : settings.ShouldCreatePlatformLobby();
        }

        /// <summary>
        /// Create a Steam lobby with the party.
        /// </summary>
        /// <param name="settings">Settings</param>
        /// <param name="create">A boolean value indicating whether a Steam lobby should be created.</param>
        /// <returns>Settings.</returns>
        public static ServerPartySettings ShouldSteamCreateLobby(this ServerPartySettings settings, bool? create)
        {
            if (create != null)
            {
                settings[SteamSettingsConstants.ShouldCreateLobby] = create.ToString()!;
            }
            else
            {
                settings.Remove(SteamSettingsConstants.ShouldCreateLobby);
            }
            return settings;
        }

        /// <summary>
        /// Should the party sync joinability with the Steam lobby.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static bool ShouldSyncJoinable(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.SyncJoinable, out var v) ? bool.Parse(v) : true;
        }

        /// <summary>
        /// Should the party sync joinability with the Steam lobby.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="sync"></param>
        /// <returns></returns>
        public static ServerPartySettings ShouldSyncJoinable(this ServerPartySettings settings, bool sync = true)
        {
            settings[SteamSettingsConstants.SyncJoinable] = sync.ToString();
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
        /// Gets if the client should join a party even if it couldn't join/create the associated steam lobby.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static bool DoNotJoinIfSteamLobbyCreationFailed(this ServerPartySettings settings)
        {
            return settings.TryGetValue(SteamSettingsConstants.DoNotJoinIfLobbyCreationFailed, out var value) ? bool.Parse(value) : false;
        }

        /// <summary>
        /// Sets a configuration value indicating if the player should join the party if steam lobby creation failed.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ServerPartySettings DoNotJoinIfSteamLobbyCreationFailed(this ServerPartySettings settings, bool value = true)
        {
             settings[SteamSettingsConstants.DoNotJoinIfLobbyCreationFailed] = value.ToString();
            return settings;
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
