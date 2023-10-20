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
using Stormancer.Server.Plugins.Models;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameSession
{
    public class GameSessionConfigurationDto
    {
        /// <summary>
        /// Group connected to gameSession
        /// </summary>
        public IEnumerable<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Game session parameters like map to launch, gameType and everything can be useful to 
        /// dedicated server.
        /// </summary>
        public JObject Parameters { get; set; } = new JObject();

        /// <summary>
        /// List of players expected in the game session.
        /// </summary>
        public IEnumerable<string> UserIds { get; set; } = Enumerable.Empty<string>();

        /// <summary>
        /// User Id of the game session's P2P host, if it has one.
        /// </summary>
        public SessionId? HostSessionId { get; set; }

        /// <summary>
        /// GameFinder that created the game session.
        /// </summary>
        public string? GameFinder { get; set; }


        /// <summary>
        /// Gets the preferred regions for hosting this game session.
        /// </summary>
        public IEnumerable<string> PreferredRegions { get;  set; } = Enumerable.Empty<string>();
    }
}
