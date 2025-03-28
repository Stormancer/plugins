﻿// MIT License
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

namespace Stormancer.Server.Plugins.Models
{
    /// <summary>
    /// Party.
    /// </summary>
    [MessagePackObject]
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
        [Key(0)]
        public string PartyId { get; set; } = "";

        /// <summary>
        /// Players in party.
        /// </summary>
        [Key(1)]
        public required Dictionary<string, Player> Players { get; set; } 


        /// <summary>
        /// Party custom data.
        /// </summary>
        [Key(2)]
        public string? CustomData { get; set; }


        /// <summary>
        /// Party creation date.
        /// </summary>
        [Key(3)]
        public DateTime CreationTimeUtc { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// Number of passes of gamefinder without finding an opponent.
        /// </summary>
        [Key(4)]
        public int PastPasses { get; set; }


        /// <summary>
        /// user id of the party leader.
        /// </summary>
        [Key(5)]
        public string PartyLeaderId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the platform id associated with the party if it's not a cross play party.
        /// </summary>
        /// <remarks>The property is set to null if the party is in cross play mode.</remarks>
        [Key(6)]
        public string? Platform { get; set; }

        /// <summary>
        /// Temporary data storage
        /// </summary>
        [IgnoreMember]
        public Dictionary<string, object> CacheStorage { get; set; } = new Dictionary<string, object>();
    }
}
