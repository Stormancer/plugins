using Stormancer.Server.Plugins.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                    catch(OperationCanceledException)
                    {

                    }
                    break;
 
            }
        }
    }
}
