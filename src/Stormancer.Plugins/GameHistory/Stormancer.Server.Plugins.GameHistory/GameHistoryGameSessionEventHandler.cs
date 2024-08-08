using Microsoft.EntityFrameworkCore;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{

    /// <summary>
    /// Context used by <see cref="IGameHistoryEventHandler.OnAddingToHistory(OnAddingToHistoryContext)"/>
    /// </summary>
    public class OnAddingToHistoryContext
    {
        internal OnAddingToHistoryContext(ISceneHost scene, IEnumerable<UserRecord> participants)
        {
            GameSessionScene = scene;
            Participants = participants;
        }

        /// <summary>
        /// Gets the game session scene associated with the game history record.
        /// </summary>
        public ISceneHost GameSessionScene { get; }

        /// <summary>
        /// Gets the participants to the game.
        /// </summary>
        public IEnumerable<UserRecord> Participants { get; }

        /// <summary>
        /// Gets or sets custom data stored with the record.
        /// </summary>
        public JsonObject CustomData { get; set; } = new JsonObject();
    }

    /// <summary>
    /// Context of the event <see cref="IGameHistoryEventHandler.OnAddingParticipantToGame(OnAddingParticipantToGameContext)"/>
    /// </summary>
    public class OnAddingParticipantToGameContext
    {
        internal OnAddingParticipantToGameContext(ISceneHost scene, GameHistoryRecord gameHistoryRecord,  UserRecord newParticipant)
        {
            GameSessionScene = scene;
            GameHistoryRecord = gameHistoryRecord;
            NewParticipant = newParticipant;
        }

        /// <summary>
        /// Gets the game session scene associated with the game history record.
        /// </summary>
        public ISceneHost GameSessionScene { get; }

        /// <summary>
        /// Gets the game history record.
        /// </summary>
        public GameHistoryRecord GameHistoryRecord { get; }

       

        /// <summary>
        /// Gets the participant being added to the game history.
        /// </summary>
        public UserRecord NewParticipant { get; }
    }

    /// <summary>
    /// Interface used to customize the behavior of the GameHistory plugin.
    /// </summary>
    public interface IGameHistoryEventHandler
    {
        /// <summary>
        /// Implementer can customize the json data stored in game history records.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnAddingToHistory(OnAddingToHistoryContext ctx);

        /// <summary>
        /// Event fired when a new participant is added to a game history record.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public Task OnAddingParticipantToGame(OnAddingParticipantToGameContext ctx);
    }
    internal class GameHistoryGameSessionEventHandler : IGameSessionEventHandler
    {
        private readonly GameHistoryService _service;
        private readonly DbContextAccessor _dbAccessor;
        private readonly ISceneHost _scene;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new instance of <see cref="GameHistoryGameSessionEventHandler"/>
        /// </summary>
        /// <param name="service"></param>
        /// <param name="dbAccessor"></param>
        /// <param name="scene"></param>
        /// <param name="logger"></param>
        public GameHistoryGameSessionEventHandler(
            GameHistoryService service,
            DbContextAccessor dbAccessor,
            ISceneHost scene,
            ILogger logger)
        {
            _service = service;
            _dbAccessor = dbAccessor;
            _scene = scene;
            _logger = logger;
        }
        async Task IGameSessionEventHandler.GameSessionStarting(Stormancer.Server.Plugins.GameSession.GameSessionContext ctx)
        {
            var dbCtx = await _dbAccessor.GetDbContextAsync();
            var onAddingToHistoryContext = new OnAddingToHistoryContext(ctx.Scene, Enumerable.Empty<UserRecord>());

            await using var scope = _scene.CreateRequestScope();
            var eventHandlers = scope.ResolveAll<IGameHistoryEventHandler>();
            await eventHandlers.RunEventHandler(h => h.OnAddingToHistory(onAddingToHistoryContext), ex => { _logger.Log(LogLevel.Error, "gameHistory", $"An error occurred while executing {nameof(IGameHistoryEventHandler.OnAddingToHistory)}", ex); });

            await _service.AddToHistoryAsync(
                Guid.Parse(ctx.Service.GameSessionId),
                onAddingToHistoryContext.Participants,
                onAddingToHistoryContext.CustomData.Deserialize<JsonDocument>()!,
                DateTime.UtcNow,DateTime.MaxValue);
        }

        async Task IGameSessionEventHandler.OnClientConnected(Stormancer.Server.Plugins.GameSession.ClientConnectedContext ctx)
        {
            var dbCtx = await _dbAccessor.GetDbContextAsync();
            var user = await dbCtx.Set<UserRecord>().FindAsync(Guid.Parse(ctx.Player.Player.UserId));

            var historyRecord = await _service.GetGameHistory(Guid.Parse(ctx.GameSession.GameSessionId));

            _logger.Log(LogLevel.Info, "gameHistory", "Adding player to game history", new { historyRecord, user });
            if (user != null && historyRecord != null)
            {
                await using var scope = _scene.CreateRequestScope();
                var eventHandlers = scope.ResolveAll<IGameHistoryEventHandler>();
                var onAddingToHistoryContext = new OnAddingParticipantToGameContext(_scene, historyRecord, user);
                await eventHandlers.RunEventHandler(h => h.OnAddingParticipantToGame(onAddingToHistoryContext), ex => { _logger.Log(LogLevel.Error, "gameHistory", $"An error occurred while executing {nameof(IGameHistoryEventHandler.OnAddingToHistory)}", ex); });

                historyRecord.Participants.Add(user);

                await _service.UpdateGameHistoryRecordAsync(historyRecord);
            }


        }

        public async Task GameSessionCompleted(GameSessionCompleteCtx ctx)
        {
            var historyRecord = await _service.GetGameHistory(Guid.Parse(ctx.Service.GameSessionId));
            if (historyRecord != null)
            {

                historyRecord.CompletedOn = DateTime.UtcNow;
                await _service.UpdateGameHistoryRecordAsync(historyRecord);
            }

        }



    }
}
