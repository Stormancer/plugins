using Stormancer.Server;
using Stormancer.Server.Plugins.GameFinder;
using System;

namespace Stormancer.Plugins.Tests.ServerApp
{
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
            ctx.HostStarting += (IHost host) =>
            {
                host.ConfigureUsers(u => u.ConfigureEphemeral(b => b.Enabled()));
               
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
                    .CustomizeScene(scene=>scene.AddSocket())
                );



                
                host.ConfigureGamefinderTemplate("server-test-docker", c => c
                    .ConfigureQuickQueue(b => b
                        .GameSessionTemplate("gamesession-server-docker")
                        .TeamCount(1)
                        .TeamSize(1)
                    )
                );

                host.ConfigureServerPools(c => c.DockerPool("docker", b => b.Image("battlegoblins-server")));

                host.ConfigureGameSession("gamesession-server-docker", c => c
                    .UseGameServer(c => c
                        .PoolId("docker")
                     )
                    .CustomizeScene(scene => scene.AddSocket())
                );
            };
            ctx.HostStarted += (IHost host) =>
            {
                host.AddGamefinder("server-test", "server-test");
                host.AddGamefinder("server-test-docker", "server-test-docker");
            };
        }
    }

}
