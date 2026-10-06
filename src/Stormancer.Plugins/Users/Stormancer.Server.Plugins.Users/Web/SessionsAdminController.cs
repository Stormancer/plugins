using Microsoft.AspNetCore.Mvc;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private readonly ISceneHost scene;

        /// <summary>
        /// Creates an instance of <see cref="SessionsAdminController"/>.
        /// </summary>
        /// <param name="scene"></param>
        public SessionsAdminController(ISceneHost scene)
        {
            this.scene = scene;
        }


        /// <summary>
        /// Gets connected questions.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("")]
        public async IAsyncEnumerable<Session> GetSessions([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await using var scope = scene.CreateRequestScope();
            var sessions = scope.Resolve<IUserSessions>();
            await foreach(var session in sessions.GetSessionsAsync(cancellationToken))
            {
                yield return session;
            }
           
        }
    }
}
