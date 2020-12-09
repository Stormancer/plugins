using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly ILimits limits;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="limits"></param>
        public LimitsAdminController(ILimits limits)
        {
            this.limits = limits;
        }

        [HttpGet]
        [Route("users")]
        [Produces(typeof(UserConnectionLimitStatus))]
        public IActionResult GetUserLimitsStatus()
        {
            return Ok(limits.GetUserLimitsStatus());
        }
    }

 
}
