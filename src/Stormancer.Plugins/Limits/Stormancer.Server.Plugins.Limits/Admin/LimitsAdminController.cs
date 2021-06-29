using Microsoft.AspNetCore.Mvc;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Provides Admin information about limits.
    /// </summary>
    [ApiController]
    [Route("_limits")]
    public class LimitsAdminController :ControllerBase
    {
     
        private readonly ISceneHost scene;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="limits"></param>
        public LimitsAdminController(ISceneHost scene)
        {
          
            this.scene = scene;
        }

        /// <summary>
        /// Gets informations about user connection limits.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("users")]
        [Produces(typeof(UserConnectionLimitStatus))]
        public async Task<IActionResult> GetUserLimitsStatus(CancellationToken cancellationToken)
        {
            await using var scope = scene.CreateRequestScope();
            var limits = scope.Resolve<LimitsClient>();
            return Ok(await limits.GetConnectionLimitStatus(cancellationToken));
        }
    }

 
}
