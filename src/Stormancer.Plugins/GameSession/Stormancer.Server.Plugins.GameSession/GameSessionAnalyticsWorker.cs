using Docker.DotNet.Models;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{

    public class GameSessionsStatistics
    {
        /// <summary>
        /// Creates a GameSessionsStatistics object.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="dimensions"></param>
        public GameSessionsStatistics(IReadOnlyDictionary<string, string> dimensions, int count)
        {
            Count = count;
            Dimensions = dimensions;
        }

        /// <summary>
        /// Number of game sessions matching the criteria.
        /// </summary>
        public int Count { get; }
        IReadOnlyDictionary<string, string> Dimensions { get; }
    }

    internal class GameSessionAnalyticsWorker
    {
        private class DimensionsComparer : IEqualityComparer<IReadOnlyDictionary<string, string>>
        {
            public bool Equals(IReadOnlyDictionary<string, string>? x, IReadOnlyDictionary<string, string>? y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (y == null)
                {
                    if (x == null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (x.Count != y.Count)
                {
                    return false;
                }

                foreach (var (key, value) in x)
                {
                    if (!y.TryGetValue(key, out var v) || v != value)
                    {
                        return false;
                    }
                }

                return true;

            }

            public int GetHashCode([DisallowNull] IReadOnlyDictionary<string, string> obj)
            {
                var hashCode = new HashCode();
                foreach (var (key, value) in obj)
                {
                    hashCode.Add(key);
                    hashCode.Add(value);
                }
                return hashCode.ToHashCode();
            }
        }
        private readonly IAnalyticsService _analytics;
        private readonly GameSessionsRepository _repository;

        public GameSessionAnalyticsWorker(IAnalyticsService service, GameSessionsRepository repository)
        {
            _analytics = service;
            _repository = repository;
        }

        private static DimensionsComparer _comparer = new DimensionsComparer();
        public async Task Run(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (!cancellationToken.IsCancellationRequested)
            {


                try
                {
                    var groups = _repository.LocalGameSessions.GroupBy(s => s.Dimensions, _comparer);
                    foreach (var group in groups)
                    {
                        _analytics.Push("gamesession", "gamesession-stats",
                            JObject.FromObject(new GameSessionsStatistics(group.Key, group.Count()))
                            );
                    }
                }
                catch { }


            }
        }

        internal void AddGameSession(GameSessionService gameSessionService)
        {
            _analytics.Push("gamesession", "gameSession-created", JObject.FromObject(new
            {
                gameFinder = gameSessionService.GetGameSessionConfig()?.GameFinder,
                parameters = gameSessionService?.GetGameSessionConfig()?.Parameters,
                gameSessionId = gameSessionService.GameSessionId
            }));

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
                sessionDuration = gameSessionService.CreatedOn - DateTime.UtcNow,
                maxClientsConnected = gameSessionService.MaxClientsConnected,
                gameFinder = gameSessionService?.GetGameSessionConfig()?.GameFinder,
                parameters = gameSessionService?.GetGameSessionConfig()?.Parameters

            }));

        }

        internal void StartGamesession(GameSessionService gameSessionService)
        {

            _analytics.Push("gamesession", "gmaesession-started", JObject.FromObject(new
            {
                gamesessionId = gameSessionService.GameSessionId,
                gameFinder = gameSessionService.GetGameSessionConfig()?.GameFinder,
                parameters = gameSessionService.GetGameSessionConfig()?.Parameters
            }));
        }
    }
}
