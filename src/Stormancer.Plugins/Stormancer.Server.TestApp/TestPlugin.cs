using Stormancer.Plugins;
using Stormancer.Server.Plugins.GameFinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.TestApp
{
    class TestPlugin : IHostPlugin
    {
        public int S2S_SCENE_COUNT = 10;
        public string GetS2SSceneId(int n) => "test-s2s-" + n;

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<S2SController>();
                builder.Register<TestController>();

            };
            ctx.HostStarting += (IHost host) =>
            {

                host.ConfigureUsers(u => u.ConfigureEphemeral(b => b.Enabled()));
                host.ConfigureGamefinderTemplate("matchmaking", c => c.ConfigureQuickQueue(o => o.GameSessionTemplate("test-gamesession").TeamCount(2).TeamSize(1)));



                host.AddSceneTemplate("template-test", scene => {

                    scene.AddController<TestController>();
                });

                host.AddSceneTemplate("template-s2s", scene => {
                    scene.AddController<S2SController>();
                });

                host.AddSceneTemplate("test-gamesession", scene =>
                {
                    scene.AddGameSession();
                    scene.AddReplication();
                });

                host.AddSceneTemplate("test-connection-rejected", scene =>
                {
                    IScenePeerClient client = null;

                    scene.Connecting.Add(peer =>
                    {
                        if (Interlocked.CompareExchange(ref client, peer, null) != null)
                        {
                            throw new ClientException("Rejected");
                        }
                        return Task.CompletedTask;
                    });

                    scene.ConnectionRejected.Add(peer =>
                    {
                        if (client != null)
                        {
                            client.Send("connectionRejected", peer.SessionId);
                        }
                        return Task.CompletedTask;
                    });

                    scene.Disconnected.Add(args =>
                    {
                        if (client?.SessionId == args.Peer.SessionId)
                        {
                            client = null;
                        }
                        return Task.CompletedTask;
                    });
                });

                host.RegisterAppFunction("scenes.count", async ctx =>
                {

                    var serializer = ctx.Resolver.Resolve<ISerializer>();
                    var template = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);

                    await serializer.SerializeAsync(host.EnumerateScenes().Count(s => s.Template == template), ctx.Output, CancellationToken.None);
                });
            };

            ctx.HostStarted += (IHost host) =>
            {


                host.ConfigureUsers(u => u.ConfigureEphemeral(e => e.Enabled()));
                host.AddGamefinder("matchmaking", "matchmaking");

                host.EnsureSceneExists("test-scene", "template-test", isPublic: true, isPersistent: true);

                for (int i = 0; i < S2S_SCENE_COUNT; i++)
                {
                    host.EnsureSceneExists(GetS2SSceneId(i), "template-s2s", isPublic: false, isPersistent: true);
                }

            };


        }
    }
}
