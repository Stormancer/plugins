using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework.Storage
{
    [PrimaryKey("Id")]
    public class QuickAccessLeaderboardEntity
    {
        public string Id { get; set; }
        public string LeaderboardName { get; set; }

        public static QuickAccessLeaderboardEntity CreateEntityFromLeaderboard(QuickAccessLeaderboard leaderboard)
        {
            return new QuickAccessLeaderboardEntity
            {
                Id = leaderboard.Id,
                LeaderboardName = leaderboard.LeaderboardName
            };
        }

        public static QuickAccessLeaderboard CreateLeaderboardFromEntity(QuickAccessLeaderboardEntity record)
        {
            return new QuickAccessLeaderboard
            {
                Id = record.Id,
                LeaderboardName = record.LeaderboardName
            };
        }
    }
}

