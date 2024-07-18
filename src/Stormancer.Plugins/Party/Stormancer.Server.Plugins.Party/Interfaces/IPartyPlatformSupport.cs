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

using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party.Interfaces
{
    /// <summary>
    /// Context used to process an invitation.
    /// </summary>
    public class InvitationContext
    {
        public IPartyService Party { get; }

        /// <summary>
        /// The user who sent the invitation
        /// </summary>
        public Session Sender { get; }

        /// <summary>
        /// If the recipient is currently in game, gets their session.
        /// </summary>
        public IEnumerable<Session> RecipientSessions { get; }

        /// <summary>
        /// Gets the user associated with the recipient, if they have an account on the game server.
        /// </summary>
        public User? RecipientUser { get; }

        /// <summary>
        /// Gets the user id used to invite the recipient.
        /// </summary>
        public PlatformId RecipientUserId { get; }

        /// <summary>
        /// Token that can be cancelled by the sender to cancel the invitation.
        /// </summary>
        /// <remarks>
        /// If the invitation system that you are implementing supports cancellation, you should cancel the invitation when this token gets canceled.
        /// If your system does not support cancellation, you can ignore this token.
        /// </remarks>
        public CancellationToken InvitationCancellationToken { get; }

        internal InvitationContext(IPartyService party, Session sender,PlatformId recipientUserId, User? recipientUser, IEnumerable<Session> recipientSessions, CancellationToken invitationCancellationToken)
        {
            Party = party;
            Sender = sender;
            RecipientUserId = recipientUserId;
            RecipientUser = recipientUser;
            RecipientSessions = recipientSessions;
            InvitationCancellationToken = invitationCancellationToken;
        }
    }

    /// <summary>
    /// This interface provides platform-specific functionality for the party.
    /// </summary>
    public interface IPartyPlatformSupport
    {
        /// <summary>
        /// The name of the platform that the implementation targets.
        /// </summary>
        string PlatformName { get; }

        /// <summary>
        /// Determines if the handler supports the invitation provided
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        bool CanHandle(InvitationContext ctx);


        /// <summary>
        /// This method should provide a platform-specific implementation for sending an invitation.
        /// </summary>
        /// <remarks>
        /// This method is intended to be implemented once per platform, in platform-specific stormancer plugins.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <returns>
        /// A task that lasts as long as the invitation can be canceled.
        /// The result of the task should be <c>true</c> if the invite was accepted, <c>false</c> if it was refused.
        /// </returns>
        Task<bool> SendInvitation(InvitationContext ctx);
    }
}
