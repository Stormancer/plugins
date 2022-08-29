using MsgPack.Serialization;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Replication
{
    class ReplicationController : ControllerBase
    {
        private readonly ISceneHost scene;
        private readonly RpcService rpc;

        public ReplicationController(ISceneHost scene, RpcService rpc)
        {
            this.scene = scene;
            this.rpc = rpc;
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
                Owner = peer.SessionId,
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
                    Owner = other.SessionId,
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
                Owner = peer.SessionId,
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
                Owner = args.Peer.SessionId,
                ViewId = "authority",
                ViewPolicyId = "authority",
                FilterType = "all",
            });

            return scene.Send(new MatchAllFilter(), "Replication.ViewUpdate", s => args.Peer.Serializer().Serialize(dto, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
        }


        [Api(ApiAccess.Public, ApiType.FireForget)]
        public Task EntityUpdate(Packet<IScenePeerClient> packet)
        {
            var recipients = packet.ReadObject<IEnumerable<SessionId>>();
            var reliability = packet.ReadObject<PacketReliability>();
            return scene.Send(new MatchArrayFilter(recipients), "Replication.EntityUpdate", s =>
            {
                packet.Serializer().Serialize(packet.Connection.SessionId, s);
                packet.Stream.CopyTo(s);
            }, PacketPriority.MEDIUM_PRIORITY, reliability);
        }

        [Api(ApiAccess.Public, ApiType.FireForget)]
        public Task BroadcastMessage(IEnumerable<SessionId> recipients, PacketReliability packetReliability, Packet<IScenePeerClient> packet)
        {
           
            return scene.Send(
                new MatchArrayFilter(recipients)
                , "Replication.BroadcastMessage"
                , s =>
                {
                    
                    var serializer = packet.Connection.Serializer();
                    serializer.Serialize(packet.Connection.SessionId, s);
                    packet.Stream.CopyTo(s);
                }
                , PacketPriority.MEDIUM_PRIORITY
                , packetReliability);
        }

        [Api(ApiAccess.Public, ApiType.FireForget)]
        public Task SendMessageToAuthority(SessionId target, PacketReliability packetReliability, Packet<IScenePeerClient> packet)
        {
            return scene.Send(
                new MatchPeerFilter(target)
                , "Replication.BroadcastMessage"
                , s =>
                {
                    var serializer = packet.Connection.Serializer();
                    serializer.Serialize(packet.Connection.SessionId, s);
                    packet.Stream.CopyTo(s);
                }
                , PacketPriority.MEDIUM_PRIORITY
                , packetReliability);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task CallAuthority(SessionId target, RequestContext<IScenePeerClient> ctx)
        {
            var peer = scene.RemotePeers.FirstOrDefault(p => p.SessionId == target);
            if (peer == null)
            {
                throw new ClientException("targetDisconnected");
            }

            await foreach (var packet in rpc.Rpc("Replication.CallAuthority", peer
                , s =>
                {
                    var serializer = ctx.RemotePeer.Serializer();
                    serializer.Serialize(ctx.RemotePeer.SessionId, s);
                    ctx.InputStream.CopyTo(s);
                }
                , PacketPriority.MEDIUM_PRIORITY).ToAsyncEnumerable())
            {
                using (packet)
                {
                    await ctx.SendValue(s => packet.Stream.CopyTo(s));
                }
            }

        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task RequestAll(IEnumerable<SessionId> recipients, RequestContext<IScenePeerClient> ctx)
        {
            var channel = Channel.CreateUnbounded<(Packet<IScenePeerClient>?,(SessionId,Exception)?)>();



            static async Task ConsumeChannel(ChannelReader<(Packet<IScenePeerClient>?, (SessionId, Exception)?)> reader, RequestContext<IScenePeerClient> ctx)
            {
                await foreach (var (packet,error) in reader.ReadAllAsync(ctx.CancellationToken))
                {
                    if (packet != null)
                    {
                        using (packet)
                        {
                            await ctx.SendValue(s =>
                            {
                                var serializer = ctx.RemotePeer.Serializer();
                                serializer.Serialize(packet.Connection.SessionId, s);
                                serializer.Serialize(true, s);
                                packet.Stream.CopyTo(s);
                            });
                        }
                    }
                    else if(error!=null)
                    {
                        var sId = error.Value.Item1;
                        var ex = error.Value.Item2;
                        await ctx.SendValue(s =>
                        {
                            var serializer = ctx.RemotePeer.Serializer();
                            serializer.Serialize(sId, s);
                            serializer.Serialize(false, s);
                            serializer.Serialize(ex.Message, s);
                        });
                    }
                }

            }

            static async Task ProcessRequest(RpcService rpc, ISerializer serializer, ChannelWriter<(Packet<IScenePeerClient>?, (SessionId, Exception)?)> writer, SessionId origin, IScenePeerClient peer, byte[] input, CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (var packet in rpc.Rpc("Replication.RequestAll", peer, s =>
                     {
                         serializer.Serialize(origin, s);
                         s.Write(input);
                     }, PacketPriority.MEDIUM_PRIORITY).ToAsyncEnumerable().WithCancellation(cancellationToken))
                    {
                        await writer.WriteAsync((packet,default));
                    }
                }
                catch(Exception ex)
                {
                    await writer.WriteAsync((default,(peer.SessionId, ex)));
                }
            }

            var memStream = new MemoryStream();
            ctx.InputStream.CopyTo(memStream);

            var ops = recipients.Select(sId =>
          {
             
              var peer = scene.RemotePeers.FirstOrDefault(s => s.SessionId == sId);
              if (peer == null)
              {
                  return Task.CompletedTask;
              }
              else
              {
                  return ProcessRequest(rpc, ctx.RemotePeer.Serializer(), channel.Writer, ctx.RemotePeer.SessionId, peer, memStream.ToArray(), ctx.CancellationToken);
              }

          });


            var t1 = ConsumeChannel(channel.Reader, ctx);
            await Task.WhenAll(ops);
            await t1;

        }
    }





}
