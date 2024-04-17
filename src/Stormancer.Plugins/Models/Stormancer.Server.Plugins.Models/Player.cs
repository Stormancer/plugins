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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.Server.Plugins.Models
{
    /// <summary>
    /// A player 
    /// </summary>
    [MessagePackObject]
    public class Player
    {

        public Player()
        {
        }

        [SetsRequiredMembers]
        public Player(SessionId sessionId, string userId, byte[]? data = null)
        {
            UserId = userId;
            SessionId = sessionId;

            if (data == null)
            {
                Data = Array.Empty<byte>();
            }
            else
            {
                Data = data;
            }

        }

        [Key(0)]
        public byte[] Data { get; set; }

        [Key(1)]
        [JsonConverter(typeof(Json.SessionIdJsonConverter))]
        public required SessionId SessionId { get; set; }

        [Key(2)]
        public required string UserId { get; set; }

        [Key(3)]
        public List<LocalPlayerInfos>? LocalPlayers { get; set; } 


        /// <summary>
        /// Temporary data storage
        /// </summary>
        [IgnoreMember]
        public Dictionary<string, object> CacheStorage { get; } = new Dictionary<string, object>();
       
    }

    /// <summary>
    /// A local player in a party member.
    /// </summary>
    [MessagePackObject]
    public class LocalPlayerInfos : IEquatable<LocalPlayerInfos?>
    {
        /// <summary>
        /// Gets or sets the platform of the player.
        /// </summary>
        [Key(0)]
        public string Platform { get; set; } = default!;

        [Key(1)]
        public string StormancerUserId { get; set; } = default!;

        [Key    (2)]
        public string Pseudo { get; set; } = default!;

        [Key(3)]
        public string PlatformId { get; set; } = default!;

        [Key(4)]
        public string CustomData { get; set; } = default!;

        [Key(5)]
        public int LocalPlayerIndex { get; set; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as LocalPlayerInfos);
        }

        public bool Equals(LocalPlayerInfos? other)
        {
            return other is not null &&
                   Platform == other.Platform &&
                   StormancerUserId == other.StormancerUserId &&
                   Pseudo == other.Pseudo &&
                   PlatformId == other.PlatformId &&
                   CustomData == other.CustomData &&
                   LocalPlayerIndex == other.LocalPlayerIndex;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Platform, StormancerUserId, Pseudo, PlatformId, CustomData, LocalPlayerIndex);
        }

        public static bool operator ==(LocalPlayerInfos? left, LocalPlayerInfos? right)
        {
            return EqualityComparer<LocalPlayerInfos>.Default.Equals(left, right);
        }

        public static bool operator !=(LocalPlayerInfos? left, LocalPlayerInfos? right)
        {
            return !(left == right);
        }
    }
}
