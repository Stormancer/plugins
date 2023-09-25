
using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// Represents a report by a player on another player.
    /// </summary>
    public class PlayerReport
    {
        /// <summary>
        /// Gets or sets the id of the report.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Message associated with the report.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Custom context data in json.
        /// </summary>
        public JsonDocument Context { get; set; } = default!;

        /// <summary>
        /// Gets or sets the player reporting the other player.
        /// </summary>
        public UserRecord Reporter { get; set; } = default!;

        /// <summary>
        /// Gets or sets the player reported in the report.
        /// </summary>
        public UserRecord Reported { get; set; } = default!;

        /// <summary>
        /// Gets or sets the utc time this record was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

    }

    internal class ModelConfigurator : Stormancer.Server.Plugins.Database.EntityFrameworkCore.IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {
            modelBuilder.Entity<PlayerReport>();
        }
    }
}
