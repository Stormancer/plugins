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
    [Service(Named =true,ServiceType ="Stormancer.PartyMerger")]
    internal class PartyMergerController
    {
        private readonly PartyMergingService _service;

        public PartyMergerController(PartyMergingService service)
        {
            _service = service;
        }

        [S2SApi]
        public  Task Merge(string partyId, CancellationToken cancellationToken)
        {
            return _service.StartMergeParty(partyId, cancellationToken);
        }
    }


    /// <summary>
    /// Controller providing party merging APIs  to clients on parties.
    /// </summary>
    internal class PartyMergingController
    {
        private readonly IPartyService _party;
        private readonly PartyMergerProxy _partyMerger;

        public PartyMergingController(IPartyService party, PartyMergerProxy partyMerger)
        {
            _party = party;
            _partyMerger = partyMerger;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Start(string partyMergerId,RequestContext<IScenePeerClient> request)
        {
            var cancellationToken = request.CancellationToken;
            
            if(_party.PartyMembers.TryGetValue(request.RemotePeer.SessionId,out var member) && member.UserId == _party.State.Settings.PartyLeaderId)
            {
                try
                {
                    await _party.UpdateSettings(state =>
                    {


                        var partySettings = new PartySettingsDto(state);
                        if (partySettings.PublicServerData == null)
                        {
                            partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        partySettings.PublicServerData["stormancer.partyMerging.status"] = "InProgress";
                        partySettings.PublicServerData["stormancer.partyMerging.merger"] = partyMergerId;
                        return partySettings;


                    }, cancellationToken);


                    await _partyMerger.Merge(partyMergerId, _party.PartyId, cancellationToken);

                    await _party.UpdateSettings(state =>
                    {


                        var partySettings = new PartySettingsDto(state);
                        if (partySettings.PublicServerData == null)
                        {
                            partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        partySettings.PublicServerData["stormancer.partyMerging.status"] = "Completed";
                        return partySettings;


                    }, cancellationToken);
                }
                catch(Exception ex)
                {
                    await _party.UpdateSettings(state =>
                    {


                        var partySettings = new PartySettingsDto(state);
                        if (partySettings.PublicServerData == null)
                        {
                            partySettings.PublicServerData = new System.Collections.Generic.Dictionary<string, string>();
                        }
                        partySettings.PublicServerData["stormancer.partyMerging.status"] = "Error";
                        partySettings.PublicServerData["stormancer.partyMerging.lastError"] = ex.Message;
                        return partySettings;


                    }, cancellationToken);
                    throw;
                }
            
            }
            else
            {
                throw new ClientException("notAuthorized?reason=notLeader");
            }

        }
    }
}
