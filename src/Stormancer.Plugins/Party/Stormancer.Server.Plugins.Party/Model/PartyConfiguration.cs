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

using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Party.Model
{
    public class PartyConfiguration
    {
        /// <summary>
        /// Scene name and platform id given by client.
        /// </summary>
        public string PartyId { get; set; }

        /// <summary>
        /// Current party leader can be changed.
        /// </summary>
        public string PartyLeaderId { get; set; }

        /// <summary>
        /// Not used yet
        /// </summary>
        public bool IsPublic { get; set; }

        /// <summary>
        /// When true, only the party leader can send invitations. When false, every party member can send invitations.
        /// </summary>
        public bool OnlyLeaderCanInvite { get; set; }

        /// <summary>
        /// When true (the default), players can join the party. When false, nobody can join the party.
        /// </summary>
        public bool IsJoinable { get; set; }

        /// <summary>
        /// The name of game finder scene
        /// </summary>
        public string GameFinderName { get; set; }

        /// <summary>
        /// Game-specific data
        /// </summary>
        public string CustomData { get; set; }

        /// <summary>
        /// Client-specified settings that can be used by other server components
        /// </summary>
        public Dictionary<string, string> ServerSettings { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Server-specified data that are sent to clients (cannot be modified by clients)
        /// </summary>
        public Dictionary<string, string> PublicServerData { get; set; } = new Dictionary<string, string>();
    }
}
