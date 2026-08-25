using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework.Storage
{
    /// <summary>
    /// Score record in the database
    /// </summary>
    [PrimaryKey("LeaderboardName", "Id")]
    public class ScoreEntity : IDisposable
    {
        public string LeaderboardName { get; set; }

        public string Id { get; set; }

        [Column(TypeName = "jsonb")]
        public JsonDocument Scores { get; set; }

        public DateTime CreatedOn { get; set; }

        [Column(TypeName = "jsonb")]
        public JsonDocument Document { get; set; }

        void IDisposable.Dispose()
        {
            Scores?.Dispose();
            Document?.Dispose();
        }

        public static ScoreEntity CreateEntityFromScore(ScoreRecord scoreRecord)
        {
            return new ScoreEntity
            {
                LeaderboardName = scoreRecord.LeaderboardName,
                Id = scoreRecord.Id,
                Scores = JsonDocument.Parse(scoreRecord.Scores.ToString()),
                CreatedOn = scoreRecord.CreatedOn,
                Document = JsonDocument.Parse(scoreRecord.Document.ToString())
            };
        }

        [return: NotNullIfNotNull("scoreEntity")]
        public static ScoreRecord? CreateScoreFromEntity(ScoreEntity? scoreEntity)
        {
            if (scoreEntity == null)
            {
                return null;
            }
            return new ScoreRecord
            {
                LeaderboardName = scoreEntity.LeaderboardName,
                Id = scoreEntity.Id,
                Scores = JObject.Parse(scoreEntity.Scores.RootElement.GetRawText()!),
                CreatedOn = scoreEntity.CreatedOn,
                Document = JObject.Parse(scoreEntity.Document.RootElement.GetRawText()!)
            };
        }
    }
}
