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

namespace Stormancer.Server.Plugins.Leaderboards
{
    /// <summary>
    /// Represents a continuation query, used to navigate in a leaderboard result set.
    /// </summary>
    public class LeaderboardContinuationQuery : LeaderboardQuery
    {
        /// <summary>
        /// Creates a new <see cref="LeaderboardContinuationQuery"/> object with default values.
        /// </summary>
        public LeaderboardContinuationQuery()
        {
        }

        internal LeaderboardContinuationQuery(LeaderboardQuery parent)
        {
            StartId = parent.StartId;
            ScoreFilters = parent.ScoreFilters;
            FieldFilters = parent.FieldFilters;
            FriendsIds = parent.FriendsIds;
            Size = parent.Size;
            Skip = parent.Skip;
            Name = parent.Name;
            ScorePath = parent.ScorePath;
            FriendsOnly = parent.FriendsOnly;
            UserId = parent.UserId;
        }

        /// <summary>
        /// Is the continuation for the previous page. 
        /// </summary>
        /// <remarks>
        /// The coninuation leads to the next page if false.
        /// </remarks>
        public bool IsPrevious { get; set; }
    }
}
