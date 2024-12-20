﻿using MessagePack;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    /// <summary>
    /// Controller exposing the internal APIs of party merger scenes.
    /// </summary>
    [Service(Named = true, ServiceType = PartyMergingConstants.PARTYMERGER_SERVICE_TYPE)]
    internal class PartyMergerController : ControllerBase
    {
        private readonly PartyMergingService _service;

        public PartyMergerController(PartyMergingService service)
        {
            _service = service;
        }

        [S2SApi]
        public async Task<string?> StartMerge(string partyId)
        {
            try
            {
                return await _service.StartMergeParty(partyId, CancellationToken.None);
            }
            catch (Exception )
            {
                return null;
            }
        }

        [S2SApi]
        public void StopMerge(string partyId)
        {
            _service.StopMergeParty(partyId);
        }

        [S2SApi]
        public Task<JObject> GetStatus(bool fromAdmin)
        {
            return _service.GetStatusAsync(fromAdmin);
        }
    }

   

    /// <summary>
    /// 
    /// </summary>
    [MessagePackObject]
    public class GetPartyMergerStatusResponse
    {
        /// <summary>
        /// How long the responses remains valid in the client's cache.
        /// </summary>
        /// <remarks>
        /// In seconds.
        /// </remarks>
        [Key(0)]
        public required int MaxAge { get; init; }

        [Key(1)]
        public required JObject Data { get; init; }
       
    }



    /// <summary>
    /// Controller providing party merging APIs  to clients on parties.
    /// </summary>
    internal class PartyMergingController : ControllerBase
    {
        private readonly IPartyService _party;
        private readonly PartyMergerProxy _proxy;
        private readonly IMergingPartyService _service;


        public PartyMergingController(IMergingPartyService service, IPartyService party, PartyMergerProxy proxy)
        {

            _service = service;
            _party = party;
            _proxy = proxy;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<GetPartyMergerStatusResponse> GetMergerStatus(string partyMergerId, CancellationToken cancellationToken)
        {
            var result = await _proxy.GetStatus(partyMergerId, false, cancellationToken);
            return new GetPartyMergerStatusResponse { MaxAge = 1, Data = result };
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public void Start(string partyMergerId, RequestContext<IScenePeerClient> request)
        {
            var cancellationToken = request.CancellationToken;

            if (_party.PartyMembers.TryGetValue(request.RemotePeer.SessionId, out var member) && member.UserId == _party.State.Settings.PartyLeaderId)
            {
                _ =  _service.StartAsync(partyMergerId, cancellationToken);
            }
            else
            {
                throw new ClientException("notAuthorized?reason=notLeader");
            }

        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Stop(RequestContext<IScenePeerClient> request)
        {
           
            if (_party.PartyMembers.TryGetValue(request.RemotePeer.SessionId, out var member) && member.UserId == _party.State.Settings.PartyLeaderId)
            {
                try
                {
                    await _service.CancelMerging();
                }
                catch(OperationCanceledException)
                {

                }
                catch(AggregateException)
                {

                }
            }
            else
            {
                throw new ClientException("notAuthorized?reason=notLeader");
            }
        }
    }
}
