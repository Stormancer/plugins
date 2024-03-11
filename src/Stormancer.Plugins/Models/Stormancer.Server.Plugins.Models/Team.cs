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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Stormancer.Server.Plugins.Models
{
    /// <summary>
    /// A team in a party or game session.
    /// </summary>
    [MessagePackObject]
    public class Team
    {
        /// <summary>
        /// Creates a new Team.
        /// </summary>
        public Team()
        {
            // Default ctor for backward compatibility
        }

        /// <summary>
        /// Creates a new Team.
        /// </summary>
        /// <param name="parties"></param>
        [SetsRequiredMembers]
        public Team(params Party[] parties) : this(parties, null)
        {
        }

        /// <summary>
        /// Creates a new Team.
        /// </summary>
        /// <param name="parties"></param>
        /// <param name="customData"></param>
        [SetsRequiredMembers]
        public Team(IEnumerable<Party> parties, object? customData = null)
        {
            CustomData = customData;
            Parties.AddRange(parties);
            TeamId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Gets or sets the team id.
        /// </summary>
        [Key(0)]
        public required string TeamId { get; set; } 

        /// <summary>
        /// Gets or sets the team player list.
        /// </summary>
        [Key(1)]
        public List<Party> Parties { get; set; } = new List<Party>();

        /// <summary>
        /// Enumerates the ids of the players in the team.
        /// </summary>
        [IgnoreMember]
        public IEnumerable<string> PlayerIds { get => Parties.SelectMany(group => group.Players.Keys); }

        /// <summary>
        /// Gets or sets custom data on the team.
        /// </summary>
        [IgnoreMember]
        public object? CustomData { get; set; }

        /// <summary>
        /// Enumerates all the players in the team.
        /// </summary>
        [IgnoreMember]
        public IEnumerable<Player> AllPlayers => Parties.SelectMany(g => g.Players.Values);
    }
}
