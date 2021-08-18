using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Admin
{
    /// <summary>
    /// Provides information about sessions.
    /// </summary>
    [Route("_sessions")]
    public class SessionsAdminController : ControllerBase
    {
        private readonly IUserSessions sessions;

        /// <summary>
        /// Creates an instance of <see cref="SessionsAdminController"/>.
        /// </summary>
        /// <param name="sessions"></param>
        public SessionsAdminController(IUserSessions sessions)
        {
            this.sessions = sessions;
        }


        /// <summary>
        /// Gets connected questions.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public IAsyncEnumerable<Session> GetSessions(CancellationToken cancellationToken)
        {
            return sessions.GetSessionsAsync(cancellationToken);
        }
    }
}
