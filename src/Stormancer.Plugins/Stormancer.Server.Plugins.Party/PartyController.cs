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

using System;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Party.Dto;
using Stormancer.Server.Party.Model;

namespace Stormancer.Server.Party
{
    class PartyController : ControllerBase
    {
        public const string PROTOCOL_VERSION = "2019-23-10.1";

        private const string NotInPartyError = "party.notInParty";
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
                throw new ClientException("unauthorized");
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

        protected override Task OnConnecting(IScenePeerClient client)
        {
            return (_partyService as PartyService)?.OnConnecting(client)?? Task.CompletedTask;
        }

        protected override Task OnConnectionRejected(IScenePeerClient client)
        {
            return (_partyService as PartyService)?.OnConnectionRejected(client) ?? Task.CompletedTask;
        }

        protected override Task OnConnected(IScenePeerClient client)
        {
            return (_partyService as PartyService)?.OnConnected(client) ?? Task.CompletedTask;
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            return (_partyService as PartyService)?.OnDisconnected(args) ?? Task.CompletedTask;
        }
    }
}
