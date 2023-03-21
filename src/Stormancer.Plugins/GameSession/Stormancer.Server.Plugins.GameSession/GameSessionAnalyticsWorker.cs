using Docker.DotNet.Models;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    internal class GameSessionAnalyticsWorker
    {
        private readonly IAnalyticsService _analytics;

        public GameSessionAnalyticsWorker(IAnalyticsService service)
        {
            _analytics = service;
        }

        public async Task Run()
        {
            while (true)
            {
                
                var startTime = DateTime.UtcNow;

                try
                {
                    if (_gameSessionsCount > 0)
                    {
                        _analytics.Push("gamesession", "gameseSession-stats", JObject.FromObject(new
                        {
                            gamesessions = _gameSessionsCount
                        }));
                    }
                }
                catch { }

                var duration = DateTime.UtcNow - startTime;

                if (TimeSpan.FromSeconds(1) - duration > TimeSpan.FromMilliseconds(20))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1) - duration);
                }
            }
        }

        private int _gameSessionsCount;

        internal void AddGameSession(GameSessionService gameSessionService)
        {
            _analytics.Push("gamesession", "gameSession-created", JObject.FromObject(new
            {

                gameSessionId = gameSessionService.GameSessionId
            }));
            Interlocked.Increment(ref _gameSessionsCount);
        }

        internal void PlayerJoined(string userId, string sessionId, string gamesessionId)
        {
            _analytics.Push("gamesession", "playerJoined", JObject.FromObject(new
            {
                userId,
                gameSessionId = gamesessionId,
                sessionId = sessionId
            }));
        }

        internal void PlayerLeft(string sessionId, string gameSessionId)
        {
            _analytics.Push("gamesession", "playerLeft", JObject.FromObject(new
            {
                sessionId = sessionId,
                gameSessionId = gameSessionId
            }));
        }

        internal void RemoveGameSession(GameSessionService gameSessionService)
        {

            _analytics.Push("gamesession", "gamesession-closed", JObject.FromObject(new
            {
                gameSessionId = gameSessionService.GameSessionId,
                sessionDuration = gameSessionService.CreatedOn - DateTime.UtcNow

            }));
            Interlocked.Decrement(ref _gameSessionsCount);
        }
    }
}
