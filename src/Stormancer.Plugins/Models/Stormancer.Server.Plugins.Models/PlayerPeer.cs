namespace Stormancer.Server.Plugins.Models
{
    public class PlayerPeer
    {
        public PlayerPeer()
        {
        }

        public PlayerPeer(IScenePeerClient peer, Player player)
        {
            Peer = peer;
            Player = player;
        }

        public IScenePeerClient Peer { get; set; }

        public Player Player { get; set; }
    }
}
