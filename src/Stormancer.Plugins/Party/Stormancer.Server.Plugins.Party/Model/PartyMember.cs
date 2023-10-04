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
using Stormancer.Server.Plugins.Party.Dto;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Party.Model
{
    [MessagePackEnum(SerializationMethod = EnumSerializationMethod.ByUnderlyingValue)]
    public enum PartyMemberStatus
    {
        NotReady = 0,
        Ready = 1
    }

    [MessagePackEnum(SerializationMethod = EnumSerializationMethod.ByUnderlyingValue)]
    public enum PartyDisconnectionReason
    {
        Left = 0,
        Kicked = 1
    }

    public class PartyMember
    {
        /// <summary>
        /// Current User ID
        /// </summary>
        public string UserId { get; set; } = default!;


        /// <summary>
        /// The client peer corresponding to this user
        /// </summary>
        public IScenePeerClient? Peer { get; set; } = default!;

        /// <summary>
        /// Set to true when a player are ready to find match
        /// </summary>
        public PartyMemberStatus StatusInParty { get; set; } = PartyMemberStatus.NotReady;

        /// <summary>
        /// Custom user data populate by developer
        /// </summary>
        public byte[] UserData { get; set; } = default!;

        /// <summary>
        /// Number of players represented by this user.
        /// </summary>
        public List<Models.LocalPlayerInfos> LocalPlayers { get; set; } = default!;

        public PartyMemberConnectionStatus ConnectionStatus { get; set; }

        public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
    }

    public enum PartyMemberConnectionStatus
    {
        Disconnected = 0,
        Reservation = 1,
        Connected = 2
    }
}
