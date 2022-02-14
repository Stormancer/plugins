using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SocketApi
{
    class SocketController : ControllerBase
    {
        private readonly ISceneHost scene;
        private readonly IPeerInfosService peers;

        public SocketController(ISceneHost scene, IPeerInfosService peers)
        {
            
            this.scene = scene;
            this.peers = peers;
        }

        [Api(ApiAccess.Public, ApiType.FireForget)]
        public Task SendUnreliable(Packet<IScenePeerClient> packet)
        {
            var sessionId = packet.ReadObject<SessionId>();
            return scene.Send(new MatchPeerFilter(sessionId.ToString()), "relay.receive", s => packet.Stream.CopyTo(s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.UNRELIABLE);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<string> CreateP2PToken(SessionId target)
        {
            return peers.CreateP2pToken(target.ToString(), scene.Id);
        }
        
    }
}
