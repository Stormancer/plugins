using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    class DevServerPoolProvider : IServerPoolProvider
    {

        private readonly Func<IServerPools> pools;
        private readonly ILogger logger;

        public DevServerPoolProvider(Func<IServerPools> pools, ILogger logger)
        {

            this.pools = pools;
            this.logger = logger;
        }
        public bool TryCreate(string id, JObject config, [NotNullWhen(true)] out IServerPool? pool)
        {
            if (config["Type"]?.ToObject<string>() == "dev")
            {
                pool = new DevServerPool(id, logger);
                return true;
            }
            else
            {
                pool = default;
                return false;
            }

        }
    }

    internal class DevServerPool : IServerPool
    {
        public DevServerPool(string id, ILogger logger)
        {
            Id = id;
        }
        public string Id { get; }

        public int ServersReady => _waitingServers.Count;

        public int ServersStarting { get; private set; } = 0;

        public int ServersRunning { get; private set; }

        public int TotalServersInPool { get; private set; }

        public int PendingServerRequests => _requests.Count;

        public bool CanAcceptRequest => true;

        public int MaxServersInPool => int.MaxValue;

        public int MinServerReady => 0;

        public bool CanManage(Session session, IScenePeerClient peer)
        {
            return session.platformId.Platform == DevDedicatedServerAuthProvider.PROVIDER_NAME;
        }

        private class DevServer
        {

        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                while (_requests.TryDequeue(out var rq))
                {
                    rq.Cancel();
                }
                while (_waitingServers.TryDequeue(out var server))
                {
                    server.Cancel();

                }
            }
        }

        private class GetServerPendingRequest
        {
            public GetServerPendingRequest(string gameSessionId, GameSessionConfiguration config)
            {
                GameSessionId = gameSessionId;
                Config = config;
            }
            public void SetServer(GameServer server)
            {
                tcs.SetResult(server);
            }

            public Task<GameServer> WaitForServerAsync()
            {
                return tcs.Task;
            }

            internal void Cancel()
            {
                tcs.SetCanceled();
                GameSessionId = null;
            }

            private TaskCompletionSource<GameServer> tcs = new TaskCompletionSource<GameServer>();

            public string? GameSessionId { get; private set; }
            public GameSessionConfiguration Config { get; }
            public CancellationTokenRegistration CancellationTokenRegistration { get; internal set; }
        }

        private class RunningGameServer
        {
            public RunningGameServer(Session session, IScenePeerClient peer)
            {
                Session = session;
                Peer = peer;
            }

            public Session? Session { get; set; }
            public IScenePeerClient Peer { get; }
            public CancellationTokenRegistration CancellationTokenRegistration { get; internal set; }

            public void SetGameFound(string gameSessionId, GameSessionConfiguration config)
            {
                _tcs.TrySetResult(new GameServerStartupParameters { Config = config, GameSessionId = gameSessionId });
            }

            private TaskCompletionSource<GameServerStartupParameters> _tcs = new TaskCompletionSource<GameServerStartupParameters>();
            internal Task<GameServerStartupParameters> WaitGameSessionAsync()
            {
                return _tcs.Task;
            }

            internal void Cancel()
            {
                _tcs.TrySetCanceled();
                Peer.DisconnectFromServer("gameserver pool shutdown");
            }
        }



        private object _syncRoot = new object();
        private Queue<GetServerPendingRequest> _requests = new Queue<GetServerPendingRequest>();
        private Queue<RunningGameServer> _waitingServers = new Queue<RunningGameServer>();
        private Dictionary<string, IScenePeerClient> _connectedServers = new Dictionary<string, IScenePeerClient>();


        public Task<GameServer> WaitGameServerAsync(string gameSessionId, GameSessionConfiguration gameSessionConfig, CancellationToken cancellationToken)
        {
            var request = new GetServerPendingRequest(gameSessionId, gameSessionConfig);
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
            lock (_syncRoot)
            {

                while (_waitingServers.TryDequeue(out var gameServer))
                {
                    if (gameServer.Session is not null)
                    {
                        gameServer.SetGameFound(gameSessionId, gameSessionConfig);
                        gameServer.CancellationTokenRegistration.Unregister();
                        return Task.FromResult(new GameServer { GameServerSessionId = gameServer.Session.SessionId });

                    }
                }

                request.CancellationTokenRegistration = cancellationToken.Register(() =>
                {

                    lock (_syncRoot)
                    {
                        foreach (var rq in _requests)
                        {
                            if (rq.GameSessionId == gameSessionId)
                            {
                                rq.Cancel();
                            }
                        }
                    }
                });

                _requests.Enqueue(request);
                return request.WaitForServerAsync();

            }

        }

        /// <summary>
        /// Triggered if the game server disconnects from the app.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public Task OnGameServerDisconnected(string sessionId)
        {
            lock (_syncRoot)
            {
                foreach (var server in _waitingServers)
                {
                    if (server.Session?.SessionId == sessionId)
                    {
                        server.Session = null;
                    }
                }
                _connectedServers.Remove(sessionId);
            }
            return Task.CompletedTask;
        }

        public Task<GameServerStartupParameters?> WaitGameSessionAsync(Session session, IScenePeerClient client, CancellationToken cancellationToken)
        {

            lock (_syncRoot)
            {
                _connectedServers[session.SessionId] = client;
                while (_requests.TryDequeue(out var request))
                {
                    if (request.GameSessionId is not null)
                    {
                        request.SetServer(new GameServer { GameServerSessionId = session.SessionId });
                        request.CancellationTokenRegistration.Unregister();
                        return Task.FromResult<GameServerStartupParameters?>(new GameServerStartupParameters() { Config = request.Config, GameSessionId = request.GameSessionId });
                    }
                }

                var server = new RunningGameServer(session, client);

                server.CancellationTokenRegistration = cancellationToken.Register(() =>
                {

                    lock (_syncRoot)
                    {
                        foreach (var server in _waitingServers)
                        {
                            if (server.Session?.SessionId == session.SessionId)
                            {
                                server.Session = null;
                            }
                        }
                    }
                });

                _waitingServers.Enqueue(server);





                return server.WaitGameSessionAsync().ContinueWith(t => (GameServerStartupParameters?)t.Result);

            }
        }

        public void UpdateConfiguration(JObject config)
        {

        }

        public async Task CloseServer(string sessionId)
        {
            IScenePeerClient? client;
            lock (_syncRoot)
            {
                _connectedServers.TryGetValue(sessionId, out client);
            }
            if(client!=null)
            {
                await client.Send("ServerPool.Shutdown", _ => { }, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
            }
        }
    }
}
