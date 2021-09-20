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

using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.PartyManagement
{
    class PartyManagementController : ControllerBase
    {
        private readonly IPartyManagementService _partyService;
        private readonly IUserSessions _sessions;
        private readonly ILogger _logger;
        private readonly IEnumerable<IPartyEventHandler> _handlers;

        public PartyManagementController(
            IPartyManagementService partyService,
            IUserSessions sessions,
            ILogger logger,
            IEnumerable<IPartyEventHandler> handlers)
        {
            _partyService = partyService;
            _sessions = sessions;
            _logger = logger;
            _handlers = handlers;
        }

        public async Task CreateSession(RequestContext<IScenePeerClient> ctx)
        {
            var partyArgs = ctx.ReadObject<PartyRequestDto>();
            if (string.IsNullOrEmpty(partyArgs.GameFinderName))
            {
                throw new ClientException("party.creationFailed?reason=gameFinderNotSet");
            }
            var user = await _sessions.GetUser(ctx.RemotePeer, ctx.CancellationToken);

            if(user == null)
            {
                throw new ClientException("notAuthenticated");
            }
            var eventCtx = new PartyCreationContext(partyArgs);
            await _handlers.RunEventHandler(handler => handler.OnCreatingParty(eventCtx), ex =>
            {
                _logger.Log(LogLevel.Error, "PartyManagementController.CreateSession", "An exception was thrown by an OnCreatingParty event handler", ex);
            });

            if (!eventCtx.Accept)
            {
                _logger.Log(LogLevel.Warn, "PartyManagementController.CreateSession", "Party creation was rejected", new
                {
                    context = eventCtx,
                    userId = user.Id,
                    sessionId = ctx.RemotePeer.SessionId
                }, user.Id, ctx.RemotePeer.SessionId);

                throw new ClientException(eventCtx.ErrorMessage ?? "Bad request");
            }

            var token = await _partyService.CreateParty(partyArgs, user.Id);

            await ctx.SendValue(token);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<string> CreateConnectionTokenFromInvitationCode(string invitationCode, RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null)
            {
                throw new ClientException("notAuthenticated");
            }

            var token = await _partyService.CreateConnectionTokenFromInvitationCodeAsync(invitationCode, ctx.CancellationToken);
            if (token == null)
            {
                throw new ClientException("codeNotFound");
            }

            return token;
        }
    }
}
