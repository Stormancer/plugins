using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.Elasticsearch
{
    internal interface ILeaderboarIndexMapping
    {
        /// <summary>
        /// classes implementing the contract can alter the name of the leaderboard used to generate the index name.
        /// </summary>
        /// <remarks>
        /// Leaderboards can be stored in individual indices or merged into a single bigger index. 2 leaderboards (different leaderboardName) can be stored in the same index
        /// if <see cref="GetIndex(string)">GetIndex</see> returns the same value for both of them.
        /// </remarks>
        public interface ILeaderboardIndexMapping
        {
            /// <summary>
            /// Gets the leaderboard id that should be used for index lookup.
            /// </summary>
            /// <param name="leaderboardName">A leaderboard name</param>
            /// <returns>The id to use to locate the leaderboard index. If null, the leaderboard name will be used.</returns>
            string? GetIndex(string leaderboardName);
        }
    }
}
