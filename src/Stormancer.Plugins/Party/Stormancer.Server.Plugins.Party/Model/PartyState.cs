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

using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party.Model
{
    internal class PartyState
    {


        /// <summary>
        /// Version number for the party's state.
        /// </summary>
        /// <remarks>
        /// This number should be incremented every time a change to the party's observable state (<see cref="PartyMembers"/> and <see cref="Settings"/>) is made.
        /// Its purpose is to help clients know whether or not their local copy of the state is complete.
        /// It is embedded into every state update notification that is sent to the clients.
        /// Say we have a client that received update n with version number v. It then receives update n+1 with version number v+2.
        /// This means our client missed an update. In this case, they will make a request to the party to give them the complete state.
        /// </remarks>
        public int VersionNumber { get; set; } = 0;

        /// <summary>
        /// Current settings for the party.
        /// </summary>
        /// <remarks>
        /// Settings initially have the values that were given to <see cref="IPartyManagementService.CreateParty(PartyRequestDto, string)"/> .
        /// During the lifetime of the party, they may be updated by calls to <see cref="IPartyService.UpdateSettings(Dto.PartySettingsDto)"/> and <see cref="IPartyService.PromoteLeader(string)"/>
        /// </remarks>
        public PartyConfiguration Settings { get; set; } = default!;

        /// <summary>
        /// This version number is specific to the settings. It is incremented every time the settings are changed.
        /// </summary>
        /// <remarks>
        /// The goal of this particular version number is to prevent a race condition when clients declare themselves as ready and the settings are changed at the same time.
        /// </remarks>
        public int SettingsVersionNumber { get; set; } = 0;

        /// <summary>
        /// Peers who have been accepted into the party, but are not yet connected, and might be rejected by another plugin's Connecting handler
        /// </summary>
        public List<IScenePeerClient> PendingAcceptedPeers { get; } = new List<IScenePeerClient>();
        /// <summary>
        /// Peers who have successfully connected to the party
        /// </summary>
        public ConcurrentDictionary<string, PartyMember> PartyMembers { get; } = new ConcurrentDictionary<string, PartyMember>();

        /// <summary>
        /// This queue is used to synchronize operations on the party state.
        /// </summary>
        /// <remarks>
        /// Any operation that modifies the state must be pushed onto this queue.
        /// This is also true of operations that need to retrieve the whole state at once, such as <see cref="IPartyService.GetPartyState"/>.
        /// </remarks>
        public TaskQueue TaskQueue { get; } = new TaskQueue();

        /// <summary>
        /// The FindGame request that is currently running for this party.
        /// </summary>
        /// <remarks>
        /// When there is no running request, this will be null.
        /// </remarks>
        public Task? FindGameRequest { get; set; }
        /// <summary>
        /// This can be used to cancel <see cref="FindGameRequest"/>.
        /// </summary>
        public CancellationTokenSource? FindGameCts { get; set; }

        /// <summary>
        /// Data that can be set by other server components for bookkeeping.
        /// </summary>
        /// <remarks>
        /// For instance, this can be used to store the unique Id of a platform-specific session that mirrors the party.
        /// </remarks>
        public ConcurrentDictionary<string, object> ServerData { get; } = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Invitations that have been sent for this party, and are pending approval, refusal or cancellation.
        /// </summary>
        /// <remarks>
        /// The format is [invited user Id]=>([sender user Id]=>[(invitation system name, invitation task)]).
        /// There can be at most one pending invitation for a given sender/recipient couple at a time.
        /// If one is issued while another one exists, the existing one is canceled.
        /// </remarks>
        public Dictionary<string, ConcurrentDictionary<string, Invitation>> PendingInvitations { get; } = new Dictionary<string, ConcurrentDictionary<string, Invitation>>();

        /// <summary>
        /// Indexed json document used to search for the party.
        /// </summary>
        /// <remarks>
        /// Null if not searchable.
        /// </remarks>
        public JObject? SearchDocument { get; set; }

    }
}
