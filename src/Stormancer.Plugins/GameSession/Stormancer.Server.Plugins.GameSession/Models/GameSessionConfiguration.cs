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

using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameSession
{
    public class GameSessionConfiguration
    {
        /// <summary>
        /// True if anyone can connect to the game session.
        /// </summary>
        [MessagePackMember(0)]
        public bool Public { get; set; }

        [MessagePackMember(1)]
        public bool canRestart { get; set; }

        /// <summary>
        /// User id of the game host. In party the host user id value is the party leader.
        /// </summary>
        [MessagePackMember(2)]
        public SessionId? HostSessionId { get; set; }

        /// <summary>
        /// Group connected to gameSession
        /// </summary>

        [MessagePackIgnore]
        public IEnumerable<Team> TeamsList => Teams.ToList();

        /// <summary>
        /// The teams that are part of this game session.
        /// </summary>
        /// <remarks>
        /// This needs to be concurrent because it can be modified during a call to <see cref="IGameSessionService.OpenToGameFinder"/>, while being read from by other code.
        /// </remarks>
        [MessagePackMember(3)]
        public List<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Get the User ID of every player in Teams
        /// </summary>
        [MessagePackIgnore]
        public IEnumerable<string> UserIds { get => Teams.SelectMany(t => t.PlayerIds); }

        /// <summary>
        /// Gamesession parameters like map to launch, gameType and everything can be useful to 
        /// dedicated server.
        /// </summary>
        [MessagePackMember(4)]
        public JObject Parameters { get; set; } = new JObject();

        /// <summary>
        /// If true, the gamesession shouldn't choose a client as the host, but request a gameserver.
        /// </summary>
        /// <remarks>
        /// If set, requires <see cref="GameServerPool"/> to be set.
        /// </remarks>
        [MessagePackMember(5)]
        public bool StartGameServer { get; set; }


        /// <summary>
        /// The gameserver pool from which the game server should be requested, if StartGameServer is true.
        /// </summary>
        [MessagePackMember(6)]
        public string? GameServerPool { get; set; }

        /// <summary>
        /// Name of the gamefinder creating the gamesession, if available.
        /// </summary>
        [MessagePackMember(7)]
        public string? GameFinder { get; set; }


        /// <summary>
        /// List of preferred regions for the game session.
        /// </summary>
        /// <remarks>
        /// Empty for any region.
        /// </remarks>
        [MessagePackMember(8)]
        public IEnumerable<string> PreferredRegions { get; set; } = Enumerable.Empty<string>();
    }
}
