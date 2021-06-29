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

using Nest;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameHistory
{
    /// <summary>
    /// Game history interface
    /// </summary>
    public interface IGameHistoryService
    {
        /// <summary>
        /// Add items to the game history
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="waitForRecordIndexed">Wait for the record to be indexed in the db</param>
        /// <returns></returns>
        Task AddToHistory<T>(IEnumerable<T> item, bool waitForRecordIndexed = false) where T : HistoryRecord;

        /// <summary>
        /// Get player game history
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<GameHistorySearchResult<PlayerHistoryRecord>> GetPlayerHistory(string playerId, int count);

        /// <summary>
        /// Get player game history by cursor
        /// </summary>
        /// <param name="cursor"></param>
        /// <returns></returns>
        Task<GameHistorySearchResult<PlayerHistoryRecord>> GetPlayerHistoryByCursor(string cursor);

        /// <summary>
        /// Get a speficic game history by gameId
        /// </summary>
        /// <param name="gameId"></param>
        /// <returns></returns>
        Task<GameHistoryRecord> GetGameHistory(string gameId);

        /// <summary>
        /// Get the game history of a player
        /// </summary>
        /// <param name="playerId"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        Task<GameHistorySearchResult<GameHistoryRecord>> GetGameHistoryForPlayer(string playerId, int count);

        /// <summary>
        /// Get the game history of a player (by using a cursor)
        /// </summary>
        /// <param name="cursor"></param>
        /// <returns></returns>
        Task<GameHistorySearchResult<GameHistoryRecord>> GetGameHistoryForPlayer(string cursor);

        /// <summary>
        /// Get the ElasticSearch client
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="historyType"></param>
        /// <returns></returns>
        Task<IElasticClient> GetESClient<T>(string historyType) where T : HistoryRecord;
    }

    /// <summary>
    /// Game History Result data
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GameHistorySearchResult<T> where T : HistoryRecord
    {
        /// <summary>
        /// Game history records
        /// </summary>
        public IEnumerable<T> Documents { get; set; } = new List<T>();

        /// <summary>
        /// Previous cursor
        /// </summary>
        public string Previous { get; set; } = "";

        /// <summary>
        /// Next cursor
        /// </summary>
        public string Next { get; set; } = "";

        /// <summary>
        /// Total count of records
        /// </summary>
        public long Total { get; set; }
    }
}
