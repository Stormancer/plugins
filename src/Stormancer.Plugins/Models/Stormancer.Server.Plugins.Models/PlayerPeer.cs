namespace Stormancer.Server.Plugins.Models
{
    public class PlayerPeer
    {
        public PlayerPeer()
        {
        }

        public PlayerPeer(SessionId sessionId, Player player)
        {
            SessionId = sessionId;
            Player = player;
        }

        public SessionId SessionId { get; set; }

        public Player Player { get; set; }
    }
}
