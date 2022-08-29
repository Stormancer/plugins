using Lucene.Net.Documents;
using Lucene.Net.Index;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Queries;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    internal class QuickQueueGameSessionsLuceneStore : ILuceneDocumentStore
    {
        private readonly ILucene lucene;
        ConcurrentDictionary<string, JObject> _data = new ConcurrentDictionary<string, JObject>();

        public QuickQueueGameSessionsLuceneStore(ILucene lucene)
        {
            this.lucene = lucene;
        }

        public void Initialize()
        {
            lucene.TryCreateIndex(QuickQueueConstants.QUICKQUEUE_GAMESESSIONS_INDEX, GameSessionMapper);
        }

        public void UpdateGameSession(string id, QuickQueueGameSessionData data)
        {
            var json = JObject.FromObject(data);
            _data.AddOrUpdate(id, json, (_, _) => json);

            lucene.IndexDocument(QuickQueueConstants.QUICKQUEUE_GAMESESSIONS_INDEX, id, json);
        }


        public void DeleteGameSession(string id)
        {
            if (_data.TryRemove(id, out _))
            {
                lucene.DeleteDocument(QuickQueueConstants.QUICKQUEUE_GAMESESSIONS_INDEX, id);
            }
        }

        private IEnumerable<IIndexableField> GameSessionMapper(JObject doc)
        {
            yield return new Int64Field("targetTeamCount", doc["TargetTeamCount"].ToObject<int>(), Field.Store.NO);
            yield return new Int64Field("targetTeamSize", doc["TargetTeamSize"].ToObject<int>(), Field.Store.NO);
        }

        public IEnumerable<Document<JObject>> GetDocuments(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                if (_data.TryGetValue(id, out var value))
                {
                    yield return new Document<JObject>(id,  value );
                }
                else
                {
                    yield return new Document<JObject> ( id,  null );
                }
            }
        }

        public bool Handles(string type)
        {
            return type == QuickQueueConstants.QUICKQUEUE_GAMESESSIONS_INDEX;
        }


    }

    internal class QuickQueueGameSessionEventHandler : IGameSessionEventHandler, IDisposable
    {
        private readonly QuickQueueGameSessionsLuceneStore repository;
        private readonly IGameSessionService gs;
        private string? _id;
        private QuickQueueGameSessionData? _gameSessionData;
        private object _syncRoot = new object();
        public QuickQueueGameSessionEventHandler(QuickQueueGameSessionsLuceneStore repository, IGameSessionService gs)
        {
            this.repository = repository;
            this.gs = gs;
        }

        
        public Task GameSessionStarting(GameSessionContext ctx)
        {
            lock (_syncRoot)
            {
                var config = ctx.Config.Parameters?.ToObject<QuickQueueGameSessionConfig>();

                if (config != null && config.AllowJoinExistingGame)
                {
                    _id = ctx.Id;
                    _gameSessionData = new QuickQueueGameSessionData()
                    {
                        CreatedOn = DateTime.UtcNow,
                        TargetTeamSize = config.TeamSize,
                        TargetTeamCount = config.TeamCount,
                        Teams = new List<QuickQueueGameSessionTeamData>()
                    };

                    repository.UpdateGameSession(ctx.Id, _gameSessionData);
                }
            }
            return Task.CompletedTask;
        }



        public Task GameSessionCompleted(GameSessionCompleteCtx ctx)
        {
            lock (_syncRoot)
            {
                if (_id is not null)
                {

                    repository.DeleteGameSession(_id);
                    _id = null;
                    _gameSessionData = null;
                }
            }
            return Task.CompletedTask;
        }
        public Task OnClientConnected(ClientConnectedContext ctx)
        {
            lock (_syncRoot)
            {

                UpdateGameSessionData();

            }
            return Task.CompletedTask;
        }

        void UpdateGameSessionData()
        {
            if (_gameSessionData is not null)
            {
                Debug.Assert(_id is not null);
                _gameSessionData.Teams = this.gs.GetGameSessionConfig().Teams.Select(t => new QuickQueueGameSessionTeamData { TeamId = t.TeamId, PlayerCount = t.AllPlayers.Count() }).ToList();
                repository.UpdateGameSession(_id, _gameSessionData);
            }
        }
        public Task OnCreatedReservation(CreatedReservationContext ctx)
        {
            lock (_syncRoot)
            {
                UpdateGameSessionData();

            }
            return Task.CompletedTask;
        }

        public Task OnClientLeaving(ClientLeavingContext ctx)
        {
            lock (_syncRoot)
            {
                UpdateGameSessionData();
            }
            return Task.CompletedTask;
        }



        public Task OnReservationCancelled(ReservationCancelledContext ctx)
        {
            lock (_syncRoot)
            {
                UpdateGameSessionData();
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_id is not null)
                {
                    repository.DeleteGameSession(_id);
                    _id = null;
                    _gameSessionData = null;
                }
            }

        }
    }

}
