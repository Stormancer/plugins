using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Stores game sessions currently running on the node.
    /// </summary>
    public class GameSessionsRepository
    {
        private object _syncRoot = new object();
        private List<IGameSessionService> _gameSessionServices = new List<IGameSessionService>();
        
        internal void AddGameSession(IGameSessionService gameSession)
        {
            lock (_syncRoot)
            {
                _gameSessionServices.Add(gameSession);
            }
        }

        /// <summary>
        /// Gets a snapshot of the currently running game sessions.
        /// </summary>
        public IEnumerable<IGameSessionService> LocalGameSessions
        {
            get
            {
                lock(_syncRoot) { 
                return _gameSessionServices.ToArray();
                }
            }
        }

        internal void RemoveGameSession(IGameSessionService gameSession)
        {

            lock (_syncRoot)
            {
                _gameSessionServices.Remove(gameSession);
            }
        }
    }
}
