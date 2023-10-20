using Newtonsoft.Json.Linq;
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
        private readonly GameServerMonitoringRepository _gameServerRepository;

        /// <summary>
        /// Creates a new <see cref="GameSessionsMonitoringService"/> object.
        /// </summary>
        /// <param name="gameSessionProxy"></param>
        /// <param name="gameServerRepository"></param>
        public GameSessionsMonitoringService(GameSessionProxy gameSessionProxy,GameServerMonitoringRepository gameServerRepository)
        {
            _gameSessionProxy = gameSessionProxy;
            _gameServerRepository = gameServerRepository;
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

            }


            return status;


        }

       
    }


    public class GameServerMonitoringRepository
    {

    }
    public class GameSessionStatus
    {
        internal GameSessionStatus()
        {

        }
        public InspectLiveGameSessionResult GameSession { get; set; }
    }


}
