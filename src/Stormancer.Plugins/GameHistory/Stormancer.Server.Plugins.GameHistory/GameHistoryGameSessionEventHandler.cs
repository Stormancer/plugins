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

        public async Task GameSessionCompleted(GameSessionCompleteCtx ctx)
        {
            var dbCtx = await _dbAccessor.GetDbContextAsync();
            var participantIds = ctx.PlayerIds.Select(i => Guid.Parse(i)).ToList();
            var participants = await dbCtx.Set<UserRecord>().Where(u => participantIds.Contains(u.Id)).ToListAsync();

            if (participants.Any())
            {
                var onAddingToHistoryContext = new OnAddingToHistoryContext(ctx.Scene, participants);

                await using var scope = _scene.CreateRequestScope();
                var eventHandlers = scope.ResolveAll<IGameHistoryEventHandler>();
                await eventHandlers.RunEventHandler(h => h.OnAddingToHistory(onAddingToHistoryContext), ex => { _logger.Log(LogLevel.Error, "gameHistory", $"An error occurred while executing {nameof(IGameHistoryEventHandler.OnAddingToHistory)}", ex); });

                await _service.AddToHistoryAsync(
                    Guid.Parse(ctx.Service.GameSessionId),
                    participants,
                    onAddingToHistoryContext.CustomData.Deserialize<JsonDocument>()!,
                    ctx.Service.OnCreated, DateTime.UtcNow);
            }
        }



    }
}
