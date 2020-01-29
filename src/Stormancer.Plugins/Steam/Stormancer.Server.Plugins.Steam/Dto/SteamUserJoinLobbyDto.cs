using MsgPack.Serialization;

namespace WindJammers2Server.Plugins.Steam.Dto
{
    public class SteamUserJoinLobbyDto
    {
        [MessagePackMember(0)]
        public ulong lobbyIdSteam {get; set; }
    }
}
