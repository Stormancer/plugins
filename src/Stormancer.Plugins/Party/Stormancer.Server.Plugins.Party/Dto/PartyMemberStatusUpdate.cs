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
using Stormancer.Server.Plugins.Party.Model;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Party.Dto
{
    /// <summary>
    /// Status update for a single client, sent from the server to the clients.
    /// </summary>
    /// <remarks>
    /// Objects of this class are never sent directly ; they are always embedded in a <code>BatchStatusUpdate</code>.
    /// </remarks>
    [MessagePackObject]
    public class PartyMemberStatusUpdate
    {
        /// <summary>
        /// Gets or sets the id of the user whose update is related to.
        /// </summary>
        [Key(0)]
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the updated status of the user.
        /// </summary>
        [Key(1)]
        public PartyMemberStatus Status { get; set; }
    }

    /// <summary>
    /// Batch update message.
    /// </summary>
    [MessagePackObject]
    public class BatchStatusUpdate
    {
        /// <summary>
        /// Route for party batch updates.
        /// </summary>
        public const string Route = "party.memberStatusUpdated";

        /// <summary>
        /// Gets or sets the list of updates in the batch.
        /// </summary>
        [Key(0)]
        public List<PartyMemberStatusUpdate> UserStatus { get; set; } = new List<PartyMemberStatusUpdate>();
    }
}
