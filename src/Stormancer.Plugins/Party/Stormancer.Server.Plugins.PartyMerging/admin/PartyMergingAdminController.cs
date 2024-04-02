using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Stormancer.Core;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.GameFinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging.admin
{
    class AdminWebApiConfig : IAdminWebApiConfig
    {
        public void ConfigureApplicationParts(ApplicationPartManager apm)
        {
            apm.ApplicationParts.Add(new AssemblyPart(this.GetType().Assembly));
        }
    }

    [ApiController]
    [Route("_partyMerger")]
    internal class PartyMergingAdminController : ControllerBase
    {
        private readonly PartyMergerProxy _partyMerger;

        public PartyMergingAdminController(PartyMergerProxy partyMerger)
        {
            _partyMerger = partyMerger;
        }

        [HttpGet]
        [Route("{mergerId}/status")]
        public async Task<IActionResult> GetStatus(string mergerId, CancellationToken cancellationToken)
        {

            var metrics = await _partyMerger.GetStatus(mergerId, true, cancellationToken);

            return Ok(metrics);


        }
    }
}
