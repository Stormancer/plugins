using MsgPack.Serialization;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Lobby metadata encoded in a bearer token in the steam lobby metadata.
    /// </summary>
    public class PartyDataDto
    {
        /// <summary>
        /// User Id
        /// </summary>
        [MessagePackMember(0)]
        public string PartyId { get; set; } = "";

        /// <summary>
        /// Party Id
        /// </summary>
        [MessagePackMember(1)]
        public string LeaderUserId { get; set; } = "";

        /// <summary>
        /// Steam Id
        /// </summary>
        [MessagePackMember(2)]
        public ulong LeaderSteamId { get; set; }
    }
}
