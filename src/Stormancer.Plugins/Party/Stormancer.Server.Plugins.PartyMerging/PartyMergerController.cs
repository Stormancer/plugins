using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.Party.Dto;
using Stormancer.Server.Plugins.PartyFinder;
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
        public Task<string?> Merge(string partyId, CancellationToken cancellationToken)
        {
            return _service.StartMergeParty(partyId, cancellationToken);
        }
    }


    /// <summary>
    /// Controller providing party merging APIs  to clients on parties.
    /// </summary>
    internal class PartyMergingController : ControllerBase
    {
        private readonly IPartyService _party;
        private readonly PartyMergerProxy _partyMerger;
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;

        public PartyMergingController(IPartyService party, PartyMergerProxy partyMerger, ISceneHost scene, ISerializer serializer)
        {
            _party = party;
            _partyMerger = partyMerger;
            _scene = scene;
            _serializer = serializer;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task Start(string partyMergerId, RequestContext<IScenePeerClient> request)
        {
            var cancellationToken = request.CancellationToken;

            if (_party.PartyMembers.TryGetValue(request.RemotePeer.SessionId, out var member) && member.UserId == _party.State.Settings.PartyLeaderId)
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


                    var connectionToken = await _partyMerger.Merge(partyMergerId, _party.PartyId, cancellationToken);


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




                    async Task Send()
                    {
                        var sessionIds = _party.PartyMembers.Where(kvp => kvp.Value.ConnectionStatus == Party.Model.PartyMemberConnectionStatus.Connected).Select(kvp => kvp.Key);
                        await _scene.Send(new MatchArrayFilter(sessionIds),
                       "partyMerging.connectionToken",
                       s => _serializer.Serialize(connectionToken, s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE);
                    }

                    _ = Send();




                }
                catch (Exception ex)
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
