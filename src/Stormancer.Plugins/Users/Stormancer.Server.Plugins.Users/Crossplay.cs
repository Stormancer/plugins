using Stormancer.Abstractions.Server.GameFinder;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Cross play related APIs.
    /// </summary>
    [Service(Named = false, ServiceType = Constants.SERVICE_TYPE)]
    public class CrossplayController : ControllerBase
    {
        private readonly IUserSessions _sessions;

        /// <summary>
        /// Creates an instance of <see cref="CrossplayController"/>.
        /// </summary>
        /// <param name="sessions"></param>
        public CrossplayController(IUserSessions sessions)
        {
            _sessions = sessions;
        }
        /// <summary>
        /// Gets values indicating if cross play is enabled for each group of a list of player groups.
        /// </summary>
        /// <param name="groups"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [S2SApi]
        public async Task<List<bool>> IsCrossPlayEnabledBatchAsync(List<IEnumerable<SessionId>> groups, CancellationToken cancellationToken)
        {
            List<bool> results = new();

            foreach (var group in groups)
            {
                var sessions = await _sessions.GetSessions(group, cancellationToken);

                var crossPlayEnabled = true;
                foreach (var session in sessions)
                {

                    if (session.Value?.User != null && session.Value.User.TryGetOption<CrossplayUserOptions>(CrossplayUserOptions.SECTION, out var option))
                    {
                        crossPlayEnabled &= option.Enabled;
                    }


                }
                results.Add(crossPlayEnabled);

            }
            return results;
        }


    }


    /// <summary>
    /// User options related to cross play
    /// </summary>
    public class CrossplayUserOptions
    {

        /// <summary>
        /// key in the user option object containing the cross play options.
        /// </summary>
        public const string SECTION = "crossplay";

        /// <summary>
        /// Gets or sets a bool indicating if cross play is enabled for the player.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// A policy evaluating if 2 parties are compatible using cross play information.
    /// </summary>
    internal class CrossPlayPartyCompatibilityPolicy : IPartyCompatibilityPolicy
    {

        ///<inheritdoc/>
        public Task<CompatibilityTestResult> AreCompatible(Party party1, Party party2, object context)
        {
            if (party1.Platform != party2.Platform)
            {
                return Task.FromResult(new CompatibilityTestResult(false, "crossplay"));
            }
            else
            {
                return Task.FromResult(new CompatibilityTestResult(true));
            }

        }


    }



}
