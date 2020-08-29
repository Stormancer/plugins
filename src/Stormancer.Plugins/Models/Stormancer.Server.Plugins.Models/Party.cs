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
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Models
{
    /// <summary>
    /// Party.
    /// </summary>
    public class Party
    {
        /// <summary>
        /// Party default constructor.
        /// </summary>
        public Party()
        {
            // Default ctor for backward compatibility
        }

        /// <summary>
        /// Party constructor with player list.
        /// </summary>
        /// <param name="players"></param>
        public Party(Dictionary<string, Player> players)
        {
            Players = players;
        }

        /// <summary>
        /// Party Id.
        /// </summary>
        [MessagePackMember(0)]
        public string PartyId { get; set; } = "";

        /// <summary>
        /// Players in party.
        /// </summary>
        [MessagePackMember(1)]
        public Dictionary<string, Player> Players { get; } = new Dictionary<string, Player>();

        [MessagePackMember(2)]
        /// <summary>
        /// Party custom data.
        /// </summary>
        public string? CustomData { get; set; }

        [MessagePackMember(3)]
        /// <summary>
        /// Party creation date.
        /// </summary>
        public DateTime CreationTimeUtc { get; } = DateTime.UtcNow;

        [MessagePackMember(4)]
        /// <summary>
        /// Number of passes of gamefinder without finding an opponent.
        /// </summary>
        public int PastPasses { get; set; }

        [MessagePackMember(5)]
        /// <summary>
        /// user id of the party leader.
        /// </summary>
        public string PartyLeaderId { get; set; }
    }
}
