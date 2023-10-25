using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession.ServerPool
{
    class DevServerPoolProvider : IServerPoolProvider
    {


        private readonly ILogger logger;
        private readonly GameSessionEventsRepository _events;

        public DevServerPoolProvider(ILogger logger, GameSessionEventsRepository events)
        {

            this.logger = logger;
            _events = events;
        }
        public bool TryCreate(string id, JObject config, [NotNullWhen(true)] out IServerPool? pool)
        {
            if (config["type"]?.ToObject<string>() == "dev")
            {
                pool = new DevServerPool(id, logger, _events);
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
        public DevServerPool(string id, ILogger logger, GameSessionEventsRepository events)
        {
            Id = id;
            _events = events;
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

            public async Task<WaitGameServerResult> WaitForServerAsync()
            {
                var s = await tcs.Task;
                return new WaitGameServerResult { Value = s, Success = true };
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
        private readonly GameSessionEventsRepository _events;

        public async Task<WaitGameServerResult> TryWaitGameServerAsync(string gameSessionId, GameSessionConfiguration gameSessionConfig, CancellationToken cancellationToken)
        {
            var record = new GameSessionEvent() { GameSessionId = gameSessionId, Type = "gameserver.starting" };
            record.CustomData["pool"] = this.Id;
            record.CustomData["PoolType"] = "dev";
            _events.PostEventAsync(record);

            record = new GameSessionEvent() { GameSessionId = gameSessionId, Type = "gameserver.started" };
            record.CustomData["pool"] = this.Id;
            bool success = true;
            try
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

                            return new WaitGameServerResult { Success = true, Value = new GameServer { GameServerId = new GameServerId { PoolId = this.Id, Id = gameSessionId }, GameServerSessionId = gameServer.Session.SessionId } };

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


                }
                var result = await request.WaitForServerAsync();
                success = result.Success;
                return result;
            }
            catch (Exception ex)
            {
                success = false;
                record.CustomData["error"] = ex.ToString();
                return new WaitGameServerResult { Success = false };
               

            }
            finally
            {

                record.CustomData["success"] = success;
                _events.PostEventAsync(record);
            }

        }

        /// <summary>
        /// Triggered if the game server disconnects from the app.
        /// </summary>
        /// <param name="serverId"></param>
        /// <returns></returns>
        public Task OnGameServerDisconnected(string serverId)
        {
            lock (_syncRoot)
            {
                foreach (var server in _waitingServers)
                {
                    if (server.Session?.platformId.PlatformUserId == serverId)
                    {
                        server.Session = null;
                    }
                }
                _connectedServers.Remove(serverId);
            }
            return Task.CompletedTask;
        }

        public Task<GameServerStartupParameters?> WaitGameSessionAsync(Session session, IScenePeerClient client, CancellationToken cancellationToken)
        {

            lock (_syncRoot)
            {
                _connectedServers[session.platformId.PlatformUserId] = client;
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

        public async Task CloseServer(string serverId)
        {
            IScenePeerClient? client;
            lock (_syncRoot)
            {
                _connectedServers.TryGetValue(serverId, out client);
            }
            if (client != null)
            {
                await client.Send("ServerPool.Shutdown", _ => { }, Core.PacketPriority.MEDIUM_PRIORITY, Core.PacketReliability.RELIABLE);
                await client.DisconnectFromServer("server.shutdown");
            }
        }

        /// <summary>
        /// Queries the logs of a game servers. 
        /// </summary>
        /// <remarks>
        /// Not supported.
        /// </remarks>
        /// <param name="gameSessionId"></param>
        /// <param name="since"></param>
        /// <param name="until"></param>
        /// <param name="size"></param>
        /// <param name="follow"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IAsyncEnumerable<string> QueryLogsAsync(string gameSessionId, DateTime? since, DateTime? until, uint size, bool follow,CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Empty<string>();
        }
    }
}
