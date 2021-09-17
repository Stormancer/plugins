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

using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.Party.Model;
using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    class PartyController : ControllerBase
    {
        public const string PROTOCOL_VERSION = "2020-01-28.1";

        private const string NotInPartyError = "party.notInParty";
        private const string UnauthorizedError = "party.unauthorized";
        private readonly IPartyService _partyService;
        private readonly ISerializer _serializer;

        public PartyController(
            IPartyService partyService,
            ISerializer serializer)
        {
            _partyService = partyService;
            _serializer = serializer;
        }

        public async Task UpdatePartySettings(RequestContext<IScenePeerClient> ctx)
        {
            var partySettings = ctx.ReadObject<PartySettingsDto>();
            PartyMember member;
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out member))
            {
                throw new ClientException(NotInPartyError);
            }

            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException(UnauthorizedError);
            }
            await _partyService.UpdateSettings(partySettings);
        }

        public Task UpdatePartyUserData(RequestContext<IScenePeerClient> ctx)
        {
            PartyMember member;
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out member))
            {
                throw new ClientException(NotInPartyError);
            }
            var data = _serializer.Deserialize<byte[]>(ctx.InputStream);

            _partyService.UpdatePartyUserData(member.UserId, data);
            return Task.CompletedTask;
        }

        public Task PromoteLeader(RequestContext<IScenePeerClient> ctx)
        {
            PartyMember member;
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out member))
            {
                throw new ClientException(NotInPartyError);
            }
            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            var playerToPromote = ctx.ReadObject<string>();
            _partyService.PromoteLeader(playerToPromote);

            return Task.CompletedTask;
        }

        public async Task KickPlayer(RequestContext<IScenePeerClient> ctx)
        {
            PartyMember member;
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out member))
            {
                throw new ClientException(NotInPartyError);
            }
            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            var playerIdToKick = ctx.ReadObject<string>();
            await _partyService.KickPlayerByLeader(playerIdToKick);
        }

        public async Task UpdateGameFinderPlayerStatus(RequestContext<IScenePeerClient> ctx)
        {
            var newStatus = ctx.ReadObject<PartyMemberStatusUpdateRequest>();
            PartyMember member;
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out member))
            {
                throw new ClientException(NotInPartyError);
            }

            await _partyService.UpdateGameFinderPlayerStatus(member.UserId, newStatus);
        }

        public async Task GetPartyState(RequestContext<IScenePeerClient> ctx)
        {
            if (_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var user))
            {
                try
                {
                    await _partyService.SendPartyState(user.UserId);
                }
                catch (OperationCanceledException)
                {
                    throw new ClientException("Client did not respond in time, or disconnected");
                }
            }
            else
            {
                throw new ClientException(NotInPartyError);
            }
        }

        public async Task GetPartyState2(RequestContext<IScenePeerClient> ctx)
        {
            if (_partyService.PartyMembers.ContainsKey(ctx.RemotePeer.SessionId))
            {
                await _partyService.SendPartyStateAsRequestAnswer(ctx);
            }
            else
            {
                throw new ClientException(NotInPartyError);
            }
        }

        public async Task SendInvitation(RequestContext<IScenePeerClient> ctx)
        {
            if (_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var user))
            {
                if (_partyService.CanSendInvitation(user.UserId))
                {
                    var recipient = ctx.ReadObject<string>();
                    var forceStormancerInvite = ctx.ReadObject<bool>();
                    var accepted = await _partyService.SendInvitation(user.UserId, recipient, forceStormancerInvite, ctx.CancellationToken);
                    await ctx.SendValue(accepted);
                }
                else
                {
                    throw new ClientException(UnauthorizedError);
                }
            }
            else
            {
                throw new ClientException(NotInPartyError);
            }
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<string> CreateInvitationCode()
        {
            return _partyService.CreateInvitationCode();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public void CancelInvitationCode()
        {
            _partyService.CancelInvitationCode();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreateConnectionTokenFromInvitationCode(string invitationCode)
        {
            sessio
            return await _partyService.CreateConnectionTokenFromInvitationCode(invitationCode);
        }

        protected override Task OnConnecting(IScenePeerClient client)
        {
            return (_partyService as PartyService).OnConnecting(client);
        }

        protected override Task OnConnectionRejected(IScenePeerClient client)
        {
            return (_partyService as PartyService).OnConnectionRejected(client);
        }

        protected override Task OnConnected(IScenePeerClient client)
        {
            return (_partyService as PartyService).OnConnected(client);
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            return (_partyService as PartyService).OnDisconnected(args);
        }
    }
}
