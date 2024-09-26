using Stormancer.Server.Plugins.Models;
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
    internal class PartyEventHandler : IPartyEventHandler
    {
        private readonly IMergingPartyService _service;

        public PartyEventHandler(IMergingPartyService service)
        {
            _service = service;
        }
        async Task IPartyEventHandler.OnGameFinderStateChanged(Stormancer.Server.Plugins.Party.GameFinderStateChangedContext context)
        {
            
            switch (context.NewState)
            {
                case PartyGameFinderStateChange.StartPending:
                    await _service.PauseMerging();

                    break;

                case PartyGameFinderStateChange.Stopped:
                    _service.TryRestartMerging();
                    break;

                case PartyGameFinderStateChange.Started:
                    try
                    {
                        await _service.CancelMerging();
                    }
                    catch (OperationCanceledException)
                    {

                    }
                    catch(ClientException)
                    {

                    }
                    break;

            }
        }

        Task IPartyEventHandler.OnCreatingReservation(Stormancer.Server.Plugins.Party.CreateReservationContext ctx)
        {
            if (ctx.Reservation.CustomData.TryGetValue("merger", out var mergerId))
            {
                _ = ctx.Party.UpdateSettings(state =>
                {


                    var partySettings = new PartySettingsDto(state);
                    if (partySettings.PublicServerData == null)
                    {
                        partySettings.PublicServerData = new Dictionary<string, string>();
                    }
                    partySettings.PublicServerData["stormancer.partyMerging.merged"] = "true";
                    return partySettings;


                }, CancellationToken.None);
            }

            return Task.CompletedTask;
        }

        async Task IPartyEventHandler.OnQuit(Stormancer.Server.Plugins.Party.QuitPartyContext ctx)
        {
            try
            {
                if (!ctx.Party.PartyMembers.Any())
                {
                    await _service.CancelMerging();
                }
            }
            catch(ClientException)
            {
                //Ignore client exceptions.
            }
        }
    }
}
