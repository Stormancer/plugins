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

using Stormancer.Server.Plugins.Party.Interfaces;
using Stormancer.Server.Plugins.Users;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    internal class StormancerPartyPlatformSupport : IPartyPlatformSupport
    {
        private readonly IUserSessions sessions;
        private readonly ISerializer serializer;

        public StormancerPartyPlatformSupport(IUserSessions sessions, ISerializer serializer)
        {
            this.sessions = sessions;
            this.serializer = serializer;
        }

        private const string RequestRoute = "party.invite";

        public string PlatformName => "stormancer";

        public bool CanSendInviteToDisconnectedPlayer => false;

        public bool IsInvitationCompatibleWith(string platform) => true;

        public async Task<bool> SendInvitation(InvitationContext ctx)
        {
            // TODO ideally, this would use the serializer of the remote peer.
            // But we do not have access to it from here, because the peer is on the authenticator scene, while we are on the party scene.
            bool accepted = await sessions.SendRequest(RequestRoute, ctx.Sender.User.Id, ctx.RecipientUserId, s =>
            {
                serializer.Serialize(ctx.Party.Settings.PartyId, s);
            }, ctx.InvitationCancellationToken)
                .Select(response =>
                {
                    using var stream = new MemoryStream(response);
                    return serializer.Deserialize<bool>(stream);
                })
                .LastOrDefaultAsync();
            return accepted;
        }
    }
}
