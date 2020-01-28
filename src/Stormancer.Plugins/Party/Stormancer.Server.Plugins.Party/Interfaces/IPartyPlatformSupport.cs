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
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party.Interfaces
{
    public class InvitationContext
    {
        public IPartyService Party { get; }

        /// <summary>
        /// The user who sent the invitation
        /// </summary>
        public Session Sender { get; }

        /// <summary>
        /// Stormancer Id of the user to whom the invitation should be sent.
        /// </summary>
        public string RecipientUserId { get; }

        /// <summary>
        /// Token that can be cancelled by the sender to cancel the invitation.
        /// </summary>
        /// <remarks>
        /// If the invitation system that you are implementing supports cancellation, you should cancel the invitation when this token gets canceled.
        /// If your system does not support cancellation, you can ignore this token.
        /// </remarks>
        public CancellationToken InvitationCancellationToken { get; }

        internal InvitationContext(IPartyService party, Session sender, string recipientUserId, CancellationToken invitationCancellationToken)
        {
            Party = party;
            Sender = sender;
            RecipientUserId = recipientUserId;
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
        /// Whether this kind of invitations can be sent and received by users who are connected to <paramref name="platform"/>.
        /// </summary>
        /// <param name="platform"></param>
        /// <returns></returns>
        bool IsInvitationCompatibleWith(string platform);

        /// <summary>
        /// Whether this platform can send an invite to a player who isn't currently playing the game.
        /// </summary>
        bool CanSendInviteToDisconnectedPlayer { get; }

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
