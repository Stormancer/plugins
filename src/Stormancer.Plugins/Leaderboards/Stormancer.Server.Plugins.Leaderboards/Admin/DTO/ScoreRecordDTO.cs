using System;

namespace Stormancer.Server.Plugins.Leaderboards.Admin.DTO
{
    public record ScoreRecordDTO
    {
        private ScoreRecordDTO()
        {
        }

        public required object Document { get; init; }
        public required string Id { get; init; }
        public required string LeaderboardName { get; init; }
        public required object Scores { get; init; }
        public required DateTime CreatedOn { get; init; }


        public static ScoreRecordDTO FromScoreRecord(ScoreRecord record)
        {
            return new ScoreRecordDTO
            {
                Document = record.Document.ToObject(),
                Id = record.Id,
                LeaderboardName = record.LeaderboardName,
                Scores = record.Scores.ToObject(),
                CreatedOn = record.CreatedOn
            };
        }
    }
}