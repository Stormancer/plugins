using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly GameServerEventsRepository _gameServerEvents;

        /// <summary>
        /// Creates a new <see cref="GameSessionsMonitoringService"/> object.
        /// </summary>
        /// <param name="gameSessionProxy"></param>
        /// <param name="gameServerEvents"></param>
        public GameSessionsMonitoringService(GameSessionProxy gameSessionProxy,GameServerEventsRepository gameServerEvents)
        {
            _gameSessionProxy = gameSessionProxy;
            _gameServerEvents = gameServerEvents;
        }
        public IAsyncEnumerable<string> GetLogsAsync(string gameSessionId, DateTime? since, DateTime? until, uint size, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<GameSessionStatus> InspectGameSessionAsync(string gameSessionId, CancellationToken cancellationToken)
        {
            var status = new GameSessionStatus();
            try
            {
                status.GameSession = await _gameSessionProxy.Inspect(gameSessionId, cancellationToken);
            }
            catch (Exception)
            {
                status.GameServerEvents = await _gameServerEvents.GetEventsAsync(gameSessionId, cancellationToken);
            }


            return status;


        }

       
    }


    public class GameServerEventsRepository
    {
        private readonly IESClientFactory _esClientFactory;

        public GameServerEventsRepository(IESClientFactory clientFactory)
        {
            _esClientFactory = clientFactory;
        }
        internal async Task<IEnumerable<GameServerEvent>> GetEventsAsync(string gameSessionId, CancellationToken cancellationToken)
        {
            var client = await _esClientFactory.CreateClient<GameServerEvent>("gameservers");

            throw new NotImplementedException();
        }
    }
    public class GameSessionStatus
    {
        internal GameSessionStatus()
        {

        }
        public InspectLiveGameSessionResult GameSession { get; set; }
        public IEnumerable<GameServerEvent> GameServerEvents { get;  set; }
    }


}
