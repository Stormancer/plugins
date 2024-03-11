// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using MessagePack;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Leaderboards
{
    /// <summary>
    /// Supported leaderboard ordering
    /// </summary>
    public enum LeaderboardOrdering
    {
        /// <summary>
        /// The leaderboard orders results by score ascending (higher is better).
        /// </summary>
        Ascending = 0,

        /// <summary>
        /// The leaderboard orders results by scores descending (lower is better).
        /// </summary>
        Descending = 1
    }

    /// <summary>
    /// A leaderboard query passed to <see cref="ILeaderboardService.Query(LeaderboardQuery, System.Threading.CancellationToken)"/>.
    /// </summary>
    [MessagePackObject]
    public class LeaderboardQuery
    {

        /// <summary>
        /// Optional start id for the leaderboard result window. 
        /// </summary>
        /// <remarks>
        /// Starts at the top if null or empty.
        /// </remarks>
        [Key(0)]
        public string? StartId { get; set; }

        /// <summary>
        /// List of filters to apply to scores to generate the leaderboard.
        /// </summary>
        [Key(1)]
        public List<ScoreFilter>? ScoreFilters { get; set; }

        /// <summary>
        /// List of filters to apply to fields to generate the leaderboard.
        /// </summary>
        [Key(2)]
        public List<FieldFilter>? FieldFilters { get; set; }

        /// <summary>
        /// Number of leaderboard entries in the result.
        /// </summary>
        [Key(3)]
        public int Size { get; set; }

        /// <summary>
        /// Number of leaderboard entries to skip.
        /// </summary>
        [Key(4)]
        public int Skip { get; set; }

        /// <summary>
        /// Name of the leaderboard to query.
        /// </summary>
        [Key(5)]
        public string Name { get; set; } = default!;

        /// <summary>
        /// 
        /// </summary>
        [Key(6)]
        public List<string>? FriendsIds { get; set; }

        /// <summary>
        /// Gets or sets the leaderboard ordering.
        /// </summary>
        [Key(7)]
        public LeaderboardOrdering Order { get; set; } = LeaderboardOrdering.Descending;

        /// <summary>
        /// Path to the score metric used to order the leaderboard query.
        /// </summary>
        [Key(8)]
        public string ScorePath { get; set; } = default!;

        /// <summary>
        /// If sets, the query returns a leaderboard made only of a player friends.
        /// </summary>
        /// <remarks>
        /// Requirements:
        /// 
        /// - <see cref="UserId"/> must be set to the id of the user to query friends for.
        /// - For the feature to include friends on some platform, the user must be connected. 
        /// </remarks>
        [Key(9)]
        public bool FriendsOnly { get; set; }

        /// <summary>
        /// Gets or sets the id of the user that requested the leaderboard.
        /// </summary>
        [IgnoreMember]
        public string? UserId { get; set; }

        [IgnoreMember]
        internal bool Adjusted { get; set; }

        /// <summary>
        /// List of user ids generated from the intersection of FriendIds if not null or empty and the list of friends FriendsOnly is set.
        /// </summary>
        [IgnoreMember]
        public IEnumerable<string>? FilteredUserIds { get; set; }
    }
}
