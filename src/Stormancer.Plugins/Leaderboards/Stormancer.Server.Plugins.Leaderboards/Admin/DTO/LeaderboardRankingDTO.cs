namespace Stormancer.Server.Plugins.Leaderboards.Admin.DTO
{
    public record LeaderboardRankingDTO
    {
        private LeaderboardRankingDTO()
        {
        }

        public required int Ranking { get; init; }

        public required ScoreRecordDTO Document { get; init; }

        public static LeaderboardRankingDTO LeaderboardRanking(LeaderboardRanking<ScoreRecord> ranking)
        {
            return new LeaderboardRankingDTO
            {
                Ranking = ranking.Ranking,
                Document = ScoreRecordDTO.FromScoreRecord(ranking.Document)
            };
        }
    }
}