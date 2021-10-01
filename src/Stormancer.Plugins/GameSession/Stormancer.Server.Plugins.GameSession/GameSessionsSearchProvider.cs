using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Filtering;
using Stormancer.Server.Plugins.ServiceBrowser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    class GameSessionsSearchProvider : IServiceSearchProvider
    {
        private List<ISceneHost> _gameSessions = new List<ISceneHost>();
        private object _syncRoot = new object();
        private readonly FilterEngine _filterEngine;
        public GameSessionsSearchProvider()
        {
            
        }
        public IEnumerable<Document<JObject>> Filter(JObject filter, uint size)
        {
            return Enumerable.Empty<Document<JObject>>();
        }

        public bool Handles(string type) => string.Equals(type, "gamesessions");


        public void AddGameSession(ISceneHost scene)
        {
            lock(_syncRoot)
            {
                _gameSessions.Add(scene);
            }
        }

        public void RemoveGameSession(ISceneHost scene)
        {
            lock(_syncRoot)
            {
                _gameSessions.Remove(scene);
            }
        }
    }
}
