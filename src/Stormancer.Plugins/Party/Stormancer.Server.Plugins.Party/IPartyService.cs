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

using Stormancer.Plugins;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.Party.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    /// <summary>
    /// Provides functions to control the party hosted on the current scene.
    /// </summary>
    public interface IPartyService
    {
        /// <summary>
        /// Id of the party
        /// </summary>
        string PartyId { get; }

        /// <summary>
        /// Updates party settings.
        /// </summary>
        /// <param name="partySettings"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task UpdateSettings(PartySettingsDto partySettings, CancellationToken ct);

        /// <summary>
        /// Updates party settings.
        /// </summary>
        /// <param name="partySettingsUpdater">updater (return null for no change)</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task UpdateSettings(Func<PartyState,PartySettingsDto?> partySettingsUpdater, CancellationToken ct);

        /// <summary>
        /// Updates the gamefinder status of a player.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="partyUserStatus"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task UpdateGameFinderPlayerStatus(string userId, PartyMemberStatusUpdateRequest partyUserStatus, CancellationToken ct);

        /// <summary>
        /// Updates party user data.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="localPlayers"></param>
        /// <param name="data"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task UpdatePartyUserData(string userId, byte[] data, List<Models.LocalPlayerInfos> localPlayers, CancellationToken ct);

        /// <summary>
        /// Promotes an user as leader.
        /// </summary>
        /// <param name="newLeaderUserId"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task PromoteLeader(string newLeaderUserId, CancellationToken ct);

        /// <summary>
        /// Kicks a player.
        /// </summary>
        /// <param name="playerToKick"></param>
        /// <param name="allowKickLeader"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task KickPlayer(string playerToKick, bool allowKickLeader, CancellationToken ct);

        /// <summary>
        /// Send the whole party state to the given user.
        /// </summary>
        /// <remarks>
        /// The party state is sent to the user via an RPC.
        /// While this is happening, no other operation can be performed on the party.
        /// This is to ensure <paramref name="recipientUserId"/> receives an up-to-date party state.
        /// Doing otherwise could lead to a barrage of GetPartyState requests from the same client, or require complex client logic.
        /// </remarks>
        /// <param name="recipientUserId">User who will receive the party state via an RPC.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Task that completes when the RPC containing the party state that is sent to <paramref name="recipientUserId"/> completes.</returns>
        Task SendPartyState(string recipientUserId, CancellationToken cancellationToken);

        /// <summary>
        /// Send the whole party state as the answer to the given RPC in <paramref name="ctx"/>.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="SendPartyState(string,CancellationToken)"/>, this method sends the state as an answer to the calling RPC, instead of a new RPC.
        /// This allows blocking the party for a much shorter time.
        /// </remarks>
        /// <param name="ctx">Context for a client RPC requesting the party state.</param>
        /// <returns>Task that completes when the party state has been sent.</returns>
        Task SendPartyStateAsRequestAnswer(RequestContext<IScenePeerClient> ctx);

        /// <summary>
        /// Check whether the given user has the permission to send invitations to the party.
        /// </summary>
        /// <param name="senderUserId">The stormancer Id of the user</param>
        /// <returns><c>true</c> if the user can send invitations, <c>false</c> otherwise.</returns>
        bool CanSendInvitation(string senderUserId);

        /// <summary>
        /// Invite a user to this party.
        /// </summary>
        /// <param name="senderUserId">The stormancer Id of the user who sends the invitation.</param>
        /// <param name="recipientUserId">The stormancer Id of the user who will receive the invitation.</param>
        /// <param name="forceStormancerInvite">
        /// If <c>false</c>, the underlying invitation system to use for this invitation will be chosen automatically, prioritizing platform-specific systems.
        /// If <c>true</c>, only the base Stormancer invitation system will be used.
        /// </param>
        /// <param name="cancellationToken">A token with which the sender can cancel the invitation.</param>
        /// <returns>
        /// A task that lasts as long as the invitation can be canceled, if the underlying invitation system supports cancelling.
        /// The result of the task is <c>true</c> if the recipient accepted the invitation, <c>false</c> if they refused it.
        /// If the underlying invitation system doesn't support the notion of refusing an invitation, it will always be true.
        /// </returns>
        Task<bool> SendInvitation(string senderUserId, string recipientUserId, bool forceStormancerInvite, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the party configuration from scene metadata.
        /// </summary>
        /// <param name="metadata"></param>
        void SetConfiguration(Dictionary<string,object?> metadata);


        /// <summary>
        /// Gets the party configuration.
        /// </summary>
        PartyConfiguration Settings { get; }

        /// <summary>
        /// Gets the party internal state.
        /// </summary>
        PartyState State { get; }

        /// <summary>
        /// Data that can be set by other server components for bookkeeping.
        /// </summary>
        /// <remarks>
        /// For instance, this can be used to store the unique Id of a platform-specific session that mirrors the party.
        /// </remarks>
        ConcurrentDictionary<string, object> ServerData { get; }

        /// <summary>
        /// The users who are currently in the party.
        /// </summary>
        /// <remarks>
        /// This doesn't include the users that are currently in the connection process.
        /// </remarks>
        IReadOnlyDictionary<SessionId, PartyMember> PartyMembers { get; }

        /// <summary>
        /// Create an invitation code
        /// </summary>
        /// <returns></returns>
        Task<string> CreateInvitationCodeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Cancels an active invitation code.
        /// </summary>
        void CancelInvitationCode();

        /// <summary>
        /// Gets a representation of the party.
        /// </summary>
        /// <returns></returns>
        Task<Models.Party> GetModel();

        /// <summary>
        /// Tries to create a reservation in the party for a list of players.
        /// </summary>
        /// <remarks> </remarks>
        /// <param name="reservation"></param>
        /// <returns>Returns true if successful, false if the reservation couldn't be done because the players are incompatibles with the party (for instance it's full, or the OnCreatingReservation custom event failed)</returns>
        Task<bool> CreateReservation(PartyReservation reservation);

        /// <summary>
        /// Gets a value indicating if a game finder is currently running.
        /// </summary>
        bool IsGameFinderRunning { get; }


    }
}
