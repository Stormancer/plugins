using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{
    /// <summary>
    /// Join entity between <see cref="GameHistoryRecord"/> and <see cref="UserRecord"/>.
    /// </summary>
    public class UserGameHistoryRecord
    {
        /// <summary>
        /// Gets or sets the id of the user associated with the relation.
        /// </summary>
        public Guid UserRecordId { get; set; }

        /// <summary>
        /// Gets or sets the id of the game associated with the relation.
        /// </summary>
        public Guid GameHistoryRecordId { get; set; }
    }

    /// <summary>
    /// A game history record
    /// </summary>
    [PrimaryKey(nameof(Id))]
    public class GameHistoryRecord : IDisposable
    {
        /// <summary>
        /// Gets or sets the id of the record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the participants to the game.
        /// </summary>
        public List<UserRecord> Participants { get; set; } = new();

        /// <summary>
        /// Gets or sets the date the game was created, UTC.
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the date the game was completed, UTC.
        /// </summary>
        public DateTime CompletedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets custom data associated with the game.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public required JsonDocument CustomData { get; set; }

        ///<inheritdoc/>
        public void Dispose()
        {
            CustomData?.Dispose();
        }

    }

    internal class GameHistoryStorage
    {
        private readonly DbContextAccessor _dbContextAccessor;

        public GameHistoryStorage(DbContextAccessor dbContextAccessor)
        {
            _dbContextAccessor = dbContextAccessor;
        }

        public async Task AddHistoryRecordAsync(GameHistoryRecord record)
        {
            var ctx = await _dbContextAccessor.GetDbContextAsync();
            ctx.Set<GameHistoryRecord>().Add(record);

            await ctx.SaveChangesAsync();
        }

        public async Task<IEnumerable<GameHistoryRecord>> GetLatestHistoryRecordAsync(Guid participantId,int skip = 0, int limit = 20)
        {
            var ctx = await _dbContextAccessor.GetDbContextAsync();
            return await ctx.Set<GameHistoryRecord>()
                .Include(r => r.Participants)
                .Where(r => r.Participants.Any(p => p.Id == participantId))
                .Take(limit)
                .Skip(skip)
                .OrderByDescending(r=>r.CreatedOn)
                .ToListAsync();
        }

        public async Task<GameHistoryRecord?> GetGameHistoryAsync(Guid gameId)
        {
            var ctx = await _dbContextAccessor.GetDbContextAsync();

            return await ctx.Set<GameHistoryRecord>().FindAsync(gameId);
        }
    }

    internal class GameHistoryDbModelBuilder : IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {
            
            modelBuilder.Entity<GameHistoryRecord>()
                .HasMany(r=>r.Participants)
                .WithMany()
                .UsingEntity< UserGameHistoryRecord>(
                l=>l.HasOne<UserRecord>().WithMany().HasForeignKey(e=>e.UserRecordId),
                r=>r.HasOne<GameHistoryRecord>().WithMany().HasForeignKey(e=>e.GameHistoryRecordId));
        }
    }
}
