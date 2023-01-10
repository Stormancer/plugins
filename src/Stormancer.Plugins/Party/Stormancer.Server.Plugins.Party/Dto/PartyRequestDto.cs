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

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Party request Dto.
    /// </summary>
    public class PartyRequestDto
    {
        /// <summary>
        /// Platform Session Id.
        /// </summary>
        [MessagePackMember(0)]
        public string PlatformSessionId { get; set; }

        /// <summary>
        /// GameFinder name.
        /// </summary>
        [MessagePackMember(1)]
        public string GameFinderName { get; set; }

        /// <summary>
        /// Custom Data.
        /// </summary>
        [MessagePackMember(2)]
        public string CustomData { get; set; }

        /// <summary>
        /// Server settings set by client.
        /// </summary>
        [MessagePackMember(3)]
        public ServerPartySettings ServerSettings { get; set; } = new ServerPartySettings();

        /// <summary>
        /// Only leader can invite players in the party.
        /// </summary>
        [MessagePackMember(4)]
        public bool OnlyLeaderCanInvite { get; set; } = true;

        /// <summary>
        /// Party is joinable.
        /// </summary>
        [MessagePackMember(5)]
        public bool IsJoinable { get; set; } = true;

        /// <summary>
        /// Whether the party should be public or private.
        /// </summary>
        /// <remarks>
        /// This has implications for party's platform-specific functionality.
        /// A public party is always visible to other players.
        /// A private party is visible only to players who have received an invitation.
        /// </remarks>
        [MessagePackMember(6)]
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Gets or sets member data to associate the party leader with on party join.
        /// </summary>
        [MessagePackMember(7)]
        public byte[] UserData { get; set; } = new byte[0];
    }
}
