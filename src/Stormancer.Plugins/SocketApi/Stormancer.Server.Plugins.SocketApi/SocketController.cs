using Microsoft.IO;
using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SocketApi
{
    /// <summary>
    /// Controller relaying messages when no direct connection is available.
    /// </summary>
    public class SocketController : ControllerBase
    {
        private readonly ISceneHost scene;
        private readonly IPeerInfosService peers;
        private readonly ISerializer serializer;
        private readonly RecyclableMemoryStreamProvider _memoryStreamProvider;


        /// <summary>
        /// Creates a new <see cref="SocketController"/> object.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="peers"></param>
        /// <param name="serializer"></param>
        public SocketController(ISceneHost scene, IPeerInfosService peers,ISerializer serializer, RecyclableMemoryStreamProvider memoryStreamProvider)
        {
            
            this.scene = scene;
            this.peers = peers;
            this.serializer = serializer;
            _memoryStreamProvider = memoryStreamProvider;
        }

        /// <summary>
        /// Relays a message unreliably to another peer.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget)]
        public async Task SendUnreliable(Packet<IScenePeerClient> packet)
        {
            var sessionId = packet.ReadObject<SessionId>();
            using var stream = _memoryStreamProvider.GetStream();
            await packet.Stream.CopyToAsync(stream);
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            await scene.Send(new MatchPeerFilter(sessionId), "relay.receive", s =>
            {
                serializer.Serialize(packet.Connection.SessionId, s);
                stream.CopyTo(s);
            }, PacketPriority.IMMEDIATE_PRIORITY, PacketReliability.UNRELIABLE);
        }

        /// <summary>
        /// Creates a P2P direct connection token to be used by clients.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<string> CreateP2PToken(SessionId target)
        {
            return peers.CreateP2pToken(target, scene.Id);
        }
        
    }
}
