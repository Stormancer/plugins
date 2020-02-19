namespace Stormancer.Server.Plugins.Steam
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
        public const string CreateLobbyPartyServerSetting = "steam.createLobby";

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
        public const string LobbyTypePartyServerSetting = "steam.lobbyType";

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
        public const string MaxMembersPartyServerSettings = "steam.maxMembers";
    }
}
