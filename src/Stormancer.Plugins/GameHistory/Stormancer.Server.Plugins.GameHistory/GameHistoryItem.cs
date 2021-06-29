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

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.GameHistory
{
    /// <summary>
    /// History DB record
    /// </summary>
    public class HistoryRecord
    {
        /// <summary>
        /// Id of the record in DB
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Data of the game history
        /// </summary>
        public JObject Data { get; set; } = new();

        /// <summary>
        /// Date of the start of the record
        /// </summary>
        public DateTime GameStartedOn { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Date of the end of the record
        /// </summary>
        public DateTime GameEndedOn { get; set; } = DateTime.MinValue;
    }

    /// <summary>
    /// Game history DB record
    /// </summary>
    public class GameHistoryRecord : HistoryRecord
    {
        /// <summary>
        /// Players of the game
        /// </summary>
        public List<PlayerHistoryRecord> Players { get; set; } = new();

        /// <summary>
        /// Players of the game
        /// </summary>
        [Obsolete("Bad naming, use 'Players' instead.", false)]
        public List<PlayerHistoryRecord> players
        {
            get { return Players; }
            set { Players = value; }
        }

        /// <summary>
        /// Team which won the game
        /// </summary>
        public string WinningTeam { get; set; } = "";
    }

    /// <summary>
    /// Player history DB record
    /// </summary>
    public class PlayerHistoryRecord : HistoryRecord
    {
        /// <summary>
        /// Game Id
        /// </summary>
        public string GameId { get; set; } = "";

        /// <summary>
        /// Player Id
        /// </summary>
        public string PlayerId { get; set; } = "";

        /// <summary>
        /// Team Id
        /// </summary>
        public string TeamId { get; set; } = "";
    }
}
