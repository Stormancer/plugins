using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Provides API to monitor and diagnostic game sessions.
    /// </summary>
    public class GameSessionsMonitoringService
    {
        private readonly GameSessionProxy _gameSessionProxy;
        private readonly ServerPoolProxy _serverPoolProxy;
        private readonly IServerPools _pools;
        private readonly GameSessionEventsRepository _gameServerEvents;

        /// <summary>
        /// Creates a new <see cref="GameSessionsMonitoringService"/> object.
        /// </summary>
        /// <param name="gameSessionProxy"></param>
        /// <param name="serverPoolProxy"></param>
        /// <param name="pools"></param>
        /// <param name="gameServerEvents"></param>
        public GameSessionsMonitoringService(GameSessionProxy gameSessionProxy, ServerPoolProxy serverPoolProxy, IServerPools pools, GameSessionEventsRepository gameServerEvents)
        {
            _gameSessionProxy = gameSessionProxy;
            _serverPoolProxy = serverPoolProxy;
            _pools = pools;
            _gameServerEvents = gameServerEvents;
        }

        /// <summary>
        /// Queries the logs of a game server. 
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="since"></param>
        /// <param name="until"></param>
        /// <param name="size"></param>
        /// <param name="follow"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<string> QueryGameServerLogsAsync(string gameSessionId, DateTime? since, DateTime? until, uint size,bool follow,[EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var events = await _gameServerEvents.GetEventsAsync(gameSessionId, cancellationToken);

            var startedEvt = events.FirstOrDefault(evt => evt.Type == "gameserver.started");
            if (startedEvt == null)
            {
                yield break;
            }

            var poolId = startedEvt.CustomData["pool"]?.ToObject<string>();
            if (poolId == null)
            {
                yield break;
            }


            await foreach (var log in _serverPoolProxy.QueryServerLogsAsync(poolId, gameSessionId, since, until,size,follow,cancellationToken))
            {
                yield return log;
            }

        }

        /// <summary>
        /// Gets live informations about a game session.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GameSessionStatus> InspectGameSessionAsync(string gameSessionId, CancellationToken cancellationToken)
        {
            var status = new GameSessionStatus();
            try
            {
                status.GameSession = await _gameSessionProxy.Inspect(gameSessionId, cancellationToken);
            }
            catch (Exception)
            {
              
            }
            status.GameServerEvents = await _gameServerEvents.GetEventsAsync(gameSessionId, cancellationToken);

            return status;


        }


    }

    /// <summary>
    /// Manages the game session events.
    /// </summary>
    public class GameSessionEventsRepository : IDisposable
    {
        private readonly IESClientFactory _esClientFactory;
        private readonly ILogger _logger;
        private readonly PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        private bool _disposed;

        /// <summary>
        /// Creates a new <see cref="GameSessionEventsRepository"/> object.
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="logger"></param>
        public GameSessionEventsRepository(IESClientFactory clientFactory, ILogger logger)
        {
            _esClientFactory = clientFactory;
            _logger = logger;
            _ = Run();
        }

        private async Task Run()
        {
            var events = new List<GameSessionEvent>();
            while (!_disposed)
            {
                await _timer.WaitForNextTickAsync();
                if(_disposed)
                {
                    return;
                }

                try
                {
                    events.Clear();

                    var client = await _esClientFactory.CreateClient<GameSessionEvent>("gameservers");


                    while (_events.TryDequeue(out var evt))
                    {
                        events.Add(evt);
                    }
                    if (events.Any())
                    {
                        await foreach (var response in client.BulkAll(events, d => d.ContinueAfterDroppedDocuments(true)).ToAsyncEnumerable())
                        {
                        }
                    }

                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, "gamesessions.events", "An error occurred while storing the game session events.", ex);
                }
            }
        }

        private ConcurrentQueue<GameSessionEvent> _events = new ConcurrentQueue<GameSessionEvent>();
        void IDisposable.Dispose()
        {
            _disposed = true;
            _timer.Dispose();
        }

        /// <summary>
        /// Queries the game server events.
        /// </summary>
        /// <param name="gameSessionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IEnumerable<GameSessionEvent>> GetEventsAsync(string gameSessionId, CancellationToken cancellationToken)
        {
            var client = await _esClientFactory.CreateClient<GameSessionEvent>("gameservers");

            var result = await client.SearchAsync<GameSessionEvent>(d => d.Query(q => q.Term("gameSessionId.keyword", gameSessionId)));

            return result.Hits.Select(h => h.Source);
        }

        /// <summary>
        /// Posts a game session monitoring event.
        /// </summary>
        /// <param name="evt"></param>
        public void PostEventAsync(GameSessionEvent evt)
        {

            _events.Enqueue(evt);
        }
    }

    /// <summary>
    /// Status of a game session.
    /// </summary>
    public class GameSessionStatus
    {
       
        /// <summary>
        /// Gets the result of the inspect live game session request.
        /// </summary>
        public InspectLiveGameSessionResult? GameSession { get; set; }

        /// <summary>
        /// Gets the events stored for the game session.
        /// </summary>
        public IEnumerable<GameSessionEvent> GameServerEvents { get; set; } = Enumerable.Empty<GameSessionEvent>();
    }


}
