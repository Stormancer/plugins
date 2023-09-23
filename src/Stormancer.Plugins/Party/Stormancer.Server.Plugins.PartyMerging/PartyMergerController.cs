using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
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
}
