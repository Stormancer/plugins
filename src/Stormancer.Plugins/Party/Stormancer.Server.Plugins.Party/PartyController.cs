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
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    [Service(Named = true, ServiceType = PartyPlugin.PARTY_SERVICEID)]
    class PartyController : ControllerBase
    {
        public const string PROTOCOL_VERSION = "2022-06-09.1";

        private const string NotInPartyError = "party.notInParty";
        private const string UnauthorizedError = "party.unauthorized";
        private readonly ILogger _logger;
        private readonly IUserSessions sessions;
        private readonly IPartyService _partyService;
        private readonly ISerializer _serializer;

        public PartyController(
            ILogger logger,
            IUserSessions sessions,
            IPartyService partyService,
            ISerializer serializer)
        {
            _logger = logger;
            this.sessions = sessions;
            _partyService = partyService;
            _serializer = serializer;
        }

        public Task UpdatePartySettings(RequestContext<IScenePeerClient> ctx)
        {
            var partySettings = ctx.ReadObject<PartySettingsDto>();

            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }

            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException(UnauthorizedError);
            }

            return _partyService.UpdateSettings(partySettings, ctx.CancellationToken);
        }

        public Task UpdatePartyUserData(RequestContext<IScenePeerClient> ctx)
        {
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }
            var data = _serializer.Deserialize<byte[]>(ctx.InputStream);

            return _partyService.UpdatePartyUserData(member.UserId, data, member.LocalPlayers, ctx.CancellationToken) ;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task UpdatePartyUserData2(byte[] data, List<Models.LocalPlayerInfos> localPlayers, RequestContext<IScenePeerClient> ctx)
        {
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }
            

            return _partyService.UpdatePartyUserData(member.UserId, data, localPlayers, ctx.CancellationToken);
        }

        public Task PromoteLeader(RequestContext<IScenePeerClient> ctx)
        {
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }
            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            var playerToPromote = ctx.ReadObject<string>();
            return _partyService.PromoteLeader(playerToPromote, ctx.CancellationToken);

        }

        public Task KickPlayer(RequestContext<IScenePeerClient> ctx)
        {

            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }
            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            var playerIdToKick = ctx.ReadObject<string>();
            return _partyService.KickPlayer(playerIdToKick,false, ctx.CancellationToken);
        }

        public Task UpdateGameFinderPlayerStatus(RequestContext<IScenePeerClient> ctx)
        {
            var newStatus = ctx.ReadObject<PartyMemberStatusUpdateRequest>();

            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }

            return _partyService.UpdateGameFinderPlayerStatus(member.UserId, newStatus, ctx.CancellationToken);
        }

        public async Task GetPartyState(RequestContext<IScenePeerClient> ctx)
        {
            if (_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var user))
            {
                try
                {
                    await _partyService.SendPartyState(user.UserId, ctx.CancellationToken);
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
        public Task<string> CreateInvitationCode(RequestContext<IScenePeerClient> ctx)
        {
            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }

            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            return _partyService.CreateInvitationCodeAsync(ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public void CancelInvitationCode(RequestContext<IScenePeerClient> ctx)
        {

            if (!_partyService.PartyMembers.TryGetValue(ctx.RemotePeer.SessionId, out var member))
            {
                throw new ClientException(NotInPartyError);
            }
            if (member.UserId != _partyService.Settings.PartyLeaderId)
            {
                throw new ClientException("unauthorized");
            }

            _partyService.CancelInvitationCode();
        }

        [S2SApi]
        public Task UpdatePartyStatusAsync(string? expectedStatus, string newStatus, string? details, CancellationToken cancellationToken)
        {
            return _partyService.UpdateSettings(state =>
            {
                var cfg = state.Settings;
                if (expectedStatus != null && (!_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus", out var status) || status != expectedStatus))
                {
                    return null;
                }
                else
                {
                    var partySettings = new PartySettingsDto(state);
                    if (partySettings.PublicServerData == null)
                    {
                        partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                    }
                    partySettings.PublicServerData["stormancer.partyStatus"] = newStatus;
                    partySettings.PublicServerData["stormancer.partyStatus.details"] = details ?? String.Empty;
                    return partySettings;
                }

            }, cancellationToken);


        }

        [S2SApi]
        public PartyStatus GetPartyStatus(CancellationToken cancellationToken)
        {
            
            if (_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus", out var status))
            {
                var result = new PartyStatus() { Status = status };
                if (_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus.details", out var details))
                {
                    result.Details = details;
                }
                return result;
            }
            else
            {
                return new PartyStatus();
            }
        }

        [S2SApi]
        public async Task AddPartyToGameSession(string gamesessionId, CancellationToken cancellationToken)
        {
            //_logger.Log(LogLevel.Info, "party.gamesession", $"Adding gamesession {gamesessionId} from party {_partyService.PartyId}", new { });
            await _partyService.UpdateSettings(state =>
            {

               
                    var partySettings = new PartySettingsDto(state);
                    if (partySettings.PublicServerData == null)
                    {
                        partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                    }
                    partySettings.PublicServerData["stormancer.partyStatus"] = "gamesession";
                    partySettings.PublicServerData["stormancer.partyStatus.details"] = gamesessionId;
                    return partySettings;
             

            }, cancellationToken);
            //_logger.Log(LogLevel.Info, "party.gamesession", $"Added gamesession {gamesessionId} from party {_partyService.PartyId}", new { });
        }

        [S2SApi]
        public async Task RemovePartyFromGameSession(string gamesessionId,CancellationToken cancellationToken)
        {
            //_logger.Log(LogLevel.Info, "party.gamesession", $"Removing gamesession {gamesessionId} from party {_partyService.PartyId}",new { });
            while ((_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus", out var status) && status == "gamesession") 
                && (_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus.details", out var currentGs) && currentGs == gamesessionId)
                && !cancellationToken.IsCancellationRequested)
            {
                await _partyService.UpdateSettings(state =>
                {

                    if ((_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus", out var status) && status == "gamesession")
                        && (_partyService.Settings.PublicServerData.TryGetValue("stormancer.partyStatus.details", out var currentGs) && currentGs == gamesessionId))
                    {
                        var partySettings = new PartySettingsDto(state);
                        if (partySettings.PublicServerData == null)
                        {
                            partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        partySettings.PublicServerData["stormancer.partyStatus"] = string.Empty;
                        partySettings.PublicServerData["stormancer.partyStatus.details"] = string.Empty;
                        return partySettings;
                    }
                    else
                    {
                        return null;
                    }

                }, cancellationToken);

            }
            //_logger.Log(LogLevel.Info, "party.gamesession", $"Removed gamesession {gamesessionId} from party {_partyService.PartyId}", new { });
        }

        [S2SApi]
        public Task<bool> CreateReservation(PartyReservation reservation)
        {
            return _partyService.CreateReservation(reservation);
        }

        [S2SApi]
        public Task<Models.Party> GetModel()
        {
            return _partyService.GetModel();
        }

        protected override Task OnConnecting(IScenePeerClient client)
        {
            return (_partyService as PartyService)!.OnConnecting(client);
        }

        protected override Task OnConnectionRejected(IScenePeerClient client)
        {
            return (_partyService as PartyService)!.OnConnectionRejected(client);
        }

        protected override Task OnConnected(IScenePeerClient client)
        {
            return (_partyService as PartyService)!.OnConnected(client);
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            return (_partyService as PartyService)!.OnDisconnected(args);
        }

        
    }


    /// <summary>
    /// Advertised party status.
    /// </summary>
    [MessagePackObject]
    public class PartyStatus
    {
        /// <summary>
        /// Gets or sets current status
        /// </summary>
        [Key(0)]
        public string Status { get; set; } = default!;

        /// <summary>
        /// Gets or sets details about the status.
        /// </summary>
        [Key(1)]
        public string Details { get; set; } = default!;

        /// <summary>
        /// Version of the party status.
        /// </summary>
        [Key(2)]
        public ulong Version { get; set; } = 0;
    }

    [MessagePackObject]
    public class PartyReservation
    {
        [Key(0)]
        public IEnumerable<Models.Player> PartyMembers { get; set; }

        [Key(1)]
        public JObject CustomData { get; set; }
    }
}
