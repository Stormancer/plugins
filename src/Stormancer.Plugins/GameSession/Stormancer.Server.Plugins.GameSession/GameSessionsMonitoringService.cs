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
        public IAsyncEnumerable<string> GetLogsAsync(string gameSessionId, DateTime? since, DateTime? until, uint size, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public GameSessionStatus InspectGameSession(string gameSessionId)
        {
           
        }
    }
    public class GameSessionStatus
    {
        internal GameSessionStatus()
        {

        }
        public GameSessionHostStatus GameServer { get; set; }
        public GameSessionConfigurationDto Configuration { get;  }
        public JObject CurrentStatus { get; set; }
    }

    /// <summary>
    /// Informations about the host of a game session.
    /// </summary>
    public class GameSessionHostStatus
    {
       
        internal GameSessionHostStatus(bool isP2P, SessionId hostSessionId,string? serverPool)
        {
            IsP2P = isP2P;
        }
        /// <summary>
        /// Is the game a P2P game
        /// </summary>
        public bool IsP2P { get; }

        /// <summary>
        /// Gets the session id of the host.
        /// </summary>
        public SessionId HostSessionId { get; }

        /// <summary>
        /// Gets the pool of the server, if the host is a server.
        /// </summary>
        public string? ServerPool { get; }

       
    }
}
