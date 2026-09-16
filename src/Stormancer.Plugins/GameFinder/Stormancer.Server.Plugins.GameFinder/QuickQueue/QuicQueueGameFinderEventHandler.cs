using Stormancer.Gamesessions.Browser;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    internal class QuickQueueGameSessionEventHandler : IGameSessionEventHandler, IDisposable
    {
        GamesessionLuceneDocumentStore repository;
        private readonly IGameSessionService gs;
        private string? _id;
        private QuickQueueGameSessionData? _gameSessionData;
        private object _syncRoot = new object();
        public QuickQueueGameSessionEventHandler(GamesessionLuceneDocumentStore repository, IGameSessionService gs)
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

                    repository.UpdateDocument(ctx.Id, new { matchmaking = _gameSessionData }, Array.Empty<byte>());
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

                    repository.DeleteDocument(_id);
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
                repository.UpdateDocument(_id, _gameSessionData, Array.Empty<byte>());
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
                    repository.DeleteDocument(_id);
                    _id = null;
                    _gameSessionData = null;
                }
            }

        }
    }
}
