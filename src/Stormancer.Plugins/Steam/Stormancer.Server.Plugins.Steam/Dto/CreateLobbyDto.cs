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
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Steam
{
    /// <summary>
    /// Create lobby dto.
    /// </summary>
    public class CreateLobbyDto
    {
        /// <summary>
        /// Steam lobby type.
        /// </summary>
        [MessagePackMember(0)]
        public LobbyType LobbyType { get; set; } = LobbyType.Private;

        /// <summary>
        /// Max members authorized to join the lobby.
        /// </summary>
        [MessagePackMember(1)]
        public int MaxMembers { get; set; }

        /// <summary>
        /// Set Lobby to be joinable or not.
        /// </summary>
        [MessagePackMember(2)]
        public bool Joinable { get; set; }

        /// <summary>
        /// Set lobby metadata.
        /// </summary>
        /// <remarks>The steam API alterates the keys to "camelCase" format (first letter becomes lower case).</remarks>
        [MessagePackMember(3)]
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
