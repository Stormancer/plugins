using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards
{
    public interface ILeaderboardStorage
    {
        Task AddQuickAccessLeaderboard(QuickAccessLeaderboard leaderboard);
        Task ClearAllScores(string leaderboardName);
        Task ClearAllScores();
        string GetIndex(string leaderboardName);
        Task<List<QuickAccessLeaderboard>> GetQuickAccessLeaderboards();
        Task<long> GetRanking(ScoreRecord score, LeaderboardQuery filters, string leaderboardName, bool enableExaequo, CancellationToken cancellationToken);
        Task<ScoreRecord?> GetScore(string leaderboardName, string playerId);
        Task<Dictionary<string, ScoreRecord?>> GetScores(string leaderboardNames, IEnumerable<string> playerIds);
        Task<long> GetTotal(string leaderboardName, LeaderboardQuery filters, CancellationToken cancellationToken);
        Task<LeaderboardResult<ScoreRecord>> Query(LeaderboardQuery leaderboardQuery, bool enableExaequo, CancellationToken cancellationToken);
        Task RemoveEntry(string leaderboardName, string entryId);
        Task RemoveQuickAccessLeaderboard(string leaderboardName);
        Task UpdateScores(Dictionary<LeaderboardEntryId, bool> results, Func<IReadOnlyDictionary<LeaderboardEntryId, ScoreRecord>, Task<Dictionary<LeaderboardEntryId, ScoreUpdate>>> updateRecords);
    }
}
