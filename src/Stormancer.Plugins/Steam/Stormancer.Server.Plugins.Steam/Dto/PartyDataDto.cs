using MessagePack;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Lobby metadata encoded in a bearer token in the steam lobby metadata.
    /// </summary>
    [MessagePackObject]
    public class PartyDataDto
    {
        /// <summary>
        /// User Id
        /// </summary>
        [Key(0)]
        public string PartyId { get; set; } = "";

        /// <summary>
        /// Party Id
        /// </summary>
        [Key(1)]
        public string LeaderUserId { get; set; } = "";

        /// <summary>
        /// Steam Id
        /// </summary>
        [Key(2)]
        public ulong LeaderSteamId { get; set; }
    }
}
