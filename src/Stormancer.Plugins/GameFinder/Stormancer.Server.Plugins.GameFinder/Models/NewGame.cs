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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Use when a the party(ies) are joining an existing gamesession.
    /// </summary>
    public class ExistingGame : IGameCandidate
    {
        public ExistingGame(string id)
        {
            Id = id;
        }
        public string Id { get; }

        public IEnumerable<Team> Teams { get; set; } = new List<Team>();
    }

    /// <summary>
    /// Use when the party(ies) are joining a new gamesession.
    /// </summary>
    public class NewGame : IGameCandidate
    {
        #region constructors
        public NewGame()
        {
            Id = Guid.NewGuid().ToString();
        }


        #endregion

        /// <summary>
        /// Id of the game.
        /// </summary>
        [MessagePackMember(0)]
        public string Id { get; private set; }

        /// <summary>
        /// Public custom data associated with the game.
        /// </summary>
        [MessagePackMember(1)]
        public JObject PublicCustomData { get; set; }

      
        /// <summary>
        /// private custom data associated with the game.
        /// </summary>
        [MessagePackIgnore]
        public JObject PrivateCustomData { get; set; }

        /// <summary>
        /// Gets or sets the teams of players in the game.
        /// </summary>
        [MessagePackMember(2)]
        public List<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Gets a sequence of all parties in the game (whatever their team)
        /// </summary>
        [MessagePackIgnore]
        public IEnumerable<Party> AllParties => Teams.SelectMany(team => team.Parties);

        [MessagePackIgnore]
        IEnumerable<Team> IGameCandidate.Teams { get => Teams; }

        /// <summary>
        /// Gets a sequence of all players in the game.
        /// </summary>
        [MessagePackIgnore]
        public IEnumerable<Player> AllPlayers => Teams.SelectMany(team => team.AllPlayers);
    }
}
