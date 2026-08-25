using Microsoft.EntityFrameworkCore;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework.Storage
{
    internal class LeaderboardDbModelBuilder : IDbModelBuilder
    {
        public void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData)
        {
            modelBuilder.Entity<ScoreEntity>(b =>
            {
                b.HasIndex(s => s.LeaderboardName);
                b.HasIndex(s => s.CreatedOn);
                b.HasIndex(s => s.Scores)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
                b.HasIndex(s => s.Document)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
            });
            modelBuilder.Entity<QuickAccessLeaderboardEntity>();
        }
    }
}
