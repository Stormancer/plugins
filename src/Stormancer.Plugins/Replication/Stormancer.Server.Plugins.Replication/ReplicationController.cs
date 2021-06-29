using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Replication
{
    class ReplicationController : ControllerBase
    {
        private readonly ISceneHost scene;

        public ReplicationController(ISceneHost scene)
        {
            this.scene = scene;
        }

        protected override async Task OnConnected(IScenePeerClient peer)
        {
            //Temporary implementation: All peers have "matchall" authority filters,
            //Which means that they are candidate authorities for everything and monitor everything
            //for full replication.
            var filterUpdates = new FilterUpdatesBatch();

            //First add own authority filter.
            var authorityFilterUpdate = new FilterUpdate
            {
                UpdateType = UpdateType.AddOrUpdate,
                FilterData = Array.Empty<byte>(),
                Owner = SessionId.From(peer.SessionId),
                ViewId = "authority", //Declares the peer authority filter.
                IsAuthority = true,
                ViewPolicyId = "authority",
                FilterType = "all",
            };
            filterUpdates.Updates.Add(authorityFilterUpdate);



            var tasks = new List<Task>();


            //Advertise the peer that all other peers are also authorities.
            foreach (var other in this.scene.RemotePeers.Where(p => p != peer))
            {
                filterUpdates.Updates.Add(new FilterUpdate
                {
                    UpdateType = UpdateType.AddOrUpdate,
                    FilterData = Array.Empty<byte>(),
                    Owner = SessionId.From(other.SessionId),
                    ViewId = "authority", //create replication view as a candidate authority
                    IsAuthority = true,
                    ViewPolicyId = "authority",
                    FilterType = "all",
                });
            }

            tasks.Add(scene.Send(new MatchPeerFilter(peer), "Replication.ViewUpdate", s => peer.Serializer().Serialize(filterUpdates, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED));

            //Create a view update that can be sent to all other peers.
            var authorityViewFilterUpdate = new FilterUpdate
            {
                UpdateType = UpdateType.AddOrUpdate,
                FilterData = Array.Empty<byte>(),
                Owner = SessionId.From(peer.SessionId),
                ViewId = "authority",  //create replication view as a candidate authority.
                ViewPolicyId = "authority",
                FilterType = "all",
                IsAuthority = true
            };

            var existingPeerFilterUpdates = new FilterUpdatesBatch();
            existingPeerFilterUpdates.Updates.Add(authorityViewFilterUpdate);

            tasks.Add(scene.Send(new MatchArrayFilter(this.scene.RemotePeers.Where(p => p != peer)), "Replication.ViewUpdate", s => peer.Serializer().Serialize(existingPeerFilterUpdates, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED));

            await Task.WhenAll(tasks);

            await base.OnConnected(peer);
        }


        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            var dto = new FilterUpdatesBatch();
            dto.Updates.Add(new FilterUpdate
            {
                UpdateType = UpdateType.Remove,
                Owner = SessionId.From(args.Peer.SessionId),
                ViewId = "authority",
                 ViewPolicyId = "authority",
                FilterType = "all",
            });

            return scene.Send(new MatchAllFilter(), "Replication.ViewUpdate", s => args.Peer.Serializer().Serialize(dto, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
        }



        public Task EntityUpdate(Packet<IScenePeerClient> packet)
        {
            var recipients = packet.ReadObject<IEnumerable<SessionId>>();
            var reliability = packet.ReadObject<PacketReliability>();
            return scene.Send(new MatchArrayFilter(recipients.Select(s => s.ToString())), "Replication.EntityUpdate", s =>
            {
                packet.Serializer().Serialize(SessionId.From(packet.Connection.SessionId), s);
                packet.Stream.CopyTo(s);
            }, PacketPriority.MEDIUM_PRIORITY, reliability);
        }

        public async Task BroadcastMessage(IEnumerable<SessionId> recipients, )
        {

        }
    }
}
