using Microsoft.IO;
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
    public class SocketController : ControllerBase
    {
        private readonly ISceneHost scene;
        private readonly IPeerInfosService peers;
        private readonly ISerializer serializer;
        private static readonly RecyclableMemoryStreamManager
                recyclableMemoryStreamManager =
                new RecyclableMemoryStreamManager();
        public SocketController(ISceneHost scene, IPeerInfosService peers,ISerializer serializer)
        {
            
            this.scene = scene;
            this.peers = peers;
            this.serializer = serializer;
        }

        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task SendUnreliable(Packet<IScenePeerClient> packet)
        {
            var sessionId = packet.ReadObject<SessionId>();
            using var stream = recyclableMemoryStreamManager.GetStream();
            await packet.Stream.CopyToAsync(stream);
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            await scene.Send(new MatchPeerFilter(sessionId.ToString()), "relay.receive", s =>
            {
                serializer.Serialize(SessionId.From(packet.Connection.SessionId), s);
                stream.CopyTo(s);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.UNRELIABLE);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<string> CreateP2PToken(SessionId target)
        {
            return peers.CreateP2pToken(target.ToString(), scene.Id);
        }
        
    }
}
