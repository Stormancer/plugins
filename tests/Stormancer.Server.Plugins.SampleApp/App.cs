using Stormancer.Server;
using Stormancer.Server.Plugins.GameFinder;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.PartyMerging;
using System.Threading.Tasks;

namespace Stormancer.Plugins.Tests.ServerApp
{
    public class TestPartyEventHandler : IPartyEventHandler
    {
        Task IPartyEventHandler.OnCreatingParty(PartyCreationContext ctx)
        {
            ctx.PartyRequest.ServerSettings.MaxMembers(4);
            return Task.CompletedTask;
        }

    }
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new TestPlugin());
        }
    }

    public class TestPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<TestPartyEventHandler>().As<IPartyEventHandler>();
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.ConfigurePlayerParty(p => p.ResetPlayerReadyStateOn(ResetPlayerReadyStateMode.PartySettingsUpdated | ResetPlayerReadyStateMode.PartyMemberDataUpdated | ResetPlayerReadyStateMode.PartyMembersListUpdated));
                host.ConfigureUsers(u => u.ConfigureEphemeral(b => b.Enabled()).ConfigureDeviceIdentifier(b => b.Enabled()));

                host.ConfigureGamefinderTemplate("server-test", c => c
                    .ConfigureQuickQueue(b => b
                        .AllowJoinExistingGame(true)
                        .GameSessionTemplate("gamesession-server")
                        .TeamCount(2)
                        .TeamSize(1)
                    )
                );

                host.ConfigureServerPools(c => c.DevPool("dev"));

                host.ConfigureGameSession("gamesession-server", c => c
                    .UseGameServer(c => c
                        .PoolId("dev")
                     )
                    .CustomizeScene(scene => scene.AddSocket())
                );

                host.ConfigureGamefinderTemplate("server-test-docker", c => c
                    .ConfigureQuickQueue(b => b
                        .GameSessionTemplate("gamesession-server-docker")
                        .TeamCount(1)
                        .TeamSize(1)
                    )
                );

                host.ConfigureGamefinderTemplate("party-noP2P", c => c
                   .ConfigurePartyGameFinder(b => b
                       .GameSessionTemplate("gamesession-noP2P")
                       
                   )
               );

                host.ConfigureGamefinderTemplate("replication-test", c => c
                    .ConfigureQuickQueue(b => b
                        .GameSessionTemplate("gamesession-replication")
                        .TeamCount(2)
                        .TeamSize(1)
                    )
                );

                host.ConfigureGamefinderTemplate("lockstep", c => c
                   .ConfigureQuickQueue(b => b
                       .GameSessionTemplate("gamesession-lockstep")
                       .TeamCount(2)
                       .TeamSize(1)
                   )
               );

                host.ConfigureGameSession("gamesession-replication", c => c
                .CustomizeScene(scene => {
                    scene.AddEntityReplication();     
                    scene.AddSocket();
                    })
                );

                host.ConfigureGameSession("gamesession-lockstep", c => c
               .CustomizeScene(scene => {
                   scene.AddLockstep();
               }).EnablePeerDirectConnection(false)
               );

                host.ConfigureGameSession("gamesession-noP2P", c => c
                    .EnablePeerDirectConnection(false)
                );

                host.ConfigureGamefinderTemplate("joingame-test", c => c
                    .ConfigureQuickQueue(b => b
                        .AllowJoinExistingGame(true)
                        .GameSessionTemplate("gamesession-replication")
                        .TeamCount(2)
                        .TeamSize(1)
                    )
                );

                host.ConfigureGameSession("gamesession-partygame", c => c
                   .CustomizeScene(scene =>
                   {
                       scene.AddEntityReplication();
                       scene.AddSocket();
                   })
                   
                   .EnablePeerDirectConnection(false)
                );

                host.ConfigureGamefinderTemplate("joinpartygame-test", c => c
                    .ConfigurePartyGameFinder(b => b
                        .PartyLeaderIsHost(true)
                        .GameSessionTemplate("gamesession-partygame")
                    )
                );


                host.ConfigureGameSession("gamesession-disable-direct-connection", c => c

                   .EnablePeerDirectConnection(false)
                );

                host.ConfigureGamefinderTemplate("disable-direct-connection-test", c => c
                    .ConfigureQuickQueue(b => b
                        .GameSessionTemplate("gamesession-disable-direct-connection")
                        .TeamCount(2)
                        .TeamSize(1)
                    )
                );


                host.ConfigurePartyMerger("duo", c => c.Algorithm(dr=>new SimplePartyMergingAlgorithm(2)));

            };
            ctx.HostStarted += (IHost host) =>
            {

                host.AddGamefinder("lockstep", "lockstep");
                 host.AddGamefinder("party-noP2P", "party-noP2P");
                host.AddGamefinder("server-test", "server-test");
                host.AddGamefinder("server-test-docker", "server-test-docker");
                host.AddGamefinder("replication-test", "replication-test");
                host.AddGamefinder("joingame-test", "joingame-test");
                host.AddGamefinder("joinpartygame-test", "joinpartygame-test");
                host.AddGamefinder("disable-direct-connection-test", "disable-direct-connection-test");
            };
        }
    }

}
