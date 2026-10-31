using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.Admin.DTO
{
    public record LeaderboardResultDTO
    {
        private LeaderboardResultDTO()
        {
        }

        public required string LeaderboardName { get; init; }

        public required List<LeaderboardRankingDTO> Results { get; init; }

        public required string Next { get; init; }

        public required string Previous { get; init; }

        public required long Total { get; init; }

        public static LeaderboardResultDTO FromLeaderboardResult(LeaderboardResult<ScoreRecord> result)
        {
            return new LeaderboardResultDTO
            {
                LeaderboardName = result.LeaderboardName,
                Results = result.Results.Select(r => LeaderboardRankingDTO.LeaderboardRanking(r)).ToList(),
                Next = result.Next,
                Previous = result.Previous,
                Total = result.Total
            };
        }
    }
}
