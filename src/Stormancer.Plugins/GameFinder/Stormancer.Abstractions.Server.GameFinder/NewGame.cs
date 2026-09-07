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
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Use when a the party(ies) are joining an existing game session.
    /// </summary>
    public class ExistingGame : IGameCandidate
    {
        /// <summary>
        /// Creates a new <see cref="ExistingGame"/> instance.
        /// </summary>
        /// <param name="id"></param>
        public ExistingGame(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the id of the game.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets or sets the list of teams added to the game.
        /// </summary>
        public List<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Gets or sets a custom context object that is not serialized but is passed to the <see cref="IGameFinderResolver"/>.
        /// </summary>
        public object? CustomContext { get; set; }
    }

    /// <summary>
    /// Use when the party(ies) are joining a new game session.
    /// </summary>
    [MessagePackObject]
    public class NewGame : IGameCandidate
    {
        /// <summary>
        /// Creates a new <see cref="NewGame"/> object.
        /// </summary>
        public NewGame()
        {
            Id = Guid.NewGuid().ToString();
        }



        /// <summary>
        /// Id of the game.
        /// </summary>
        [Key(0)]
        public string Id { get; private set; }

        /// <summary>
        /// Public custom data associated with the game.
        /// </summary>
        [Key(1)]
        public JObject PublicCustomData { get; set; } = new JObject();

      
        /// <summary>
        /// private custom data associated with the game.
        /// </summary>
        [IgnoreMember]
        public JObject PrivateCustomData { get; set; } = new JObject();

        /// <summary>
        /// Gets or sets the teams of players in the game.
        /// </summary>
        [Key(2)]
        public List<Team> Teams { get; set; } = new List<Team>();

        /// <summary>
        /// Gets a sequence of all parties in the game (whatever their team)
        /// </summary>
        [IgnoreMember]
        public IEnumerable<Party> AllParties => Teams.SelectMany(team => team.Parties);

        [IgnoreMember]
        List<Team> IGameCandidate.Teams { get => Teams; }

        /// <summary>
        /// Gets a sequence of all players in the game.
        /// </summary>
        [IgnoreMember]
        public IEnumerable<Player> AllPlayers => Teams.SelectMany(team => team.AllPlayers);

        /// <summary>
        /// Gets or sets a custom context object that is not serialized but is passed to the <see cref="IGameFinderResolver"/>.
        /// </summary>
        [IgnoreMember]
        public object? CustomContext { get; set; }
    }
}
