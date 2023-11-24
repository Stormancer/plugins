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
        public Task<string?> StartMerge(string partyId)
        {
            return _service.StartMergeParty(partyId, CancellationToken.None);
        }

        [S2SApi]
        public void StopMerge(string partyId)
        {
            _service.StopMergeParty(partyId);
        }

        [S2SApi]
        public Task<JObject> GetStatus()
        {
            return _service.GetStatusAsync(true);
        }
    }


    /// <summary>
    /// Controller providing party merging APIs  to clients on parties.
    /// </summary>
    internal class PartyMergingController : ControllerBase
    {
        private readonly IPartyService _party;
        private readonly IMergingPartyService _service;


        public PartyMergingController(IMergingPartyService service, IPartyService party)
        {

            _service = service;
            _party = party;
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
            }
            else
            {
                throw new ClientException("notAuthorized?reason=notLeader");
            }
        }
    }
}
