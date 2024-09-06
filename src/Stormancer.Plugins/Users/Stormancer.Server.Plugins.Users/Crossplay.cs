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
        private readonly CrossplayService _crossplay;

        /// <summary>
        /// Creates an instance of <see cref="CrossplayController"/>.
        /// </summary>
        /// <param name="sessions"></param>
        public CrossplayController(IUserSessions sessions, CrossplayService crossplay)
        {
            _sessions = sessions;
            this._crossplay = crossplay;
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

                    if (session.Value?.User != null)
                    {
                        crossPlayEnabled &= _crossplay.IsCrossplayEnabled(session.Value.User);
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
        /// Gets or sets a bool indicating if cross play is enabled for the player in the application options.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }


    /// <summary>
    /// Provides a method to evaluate if cross play is enabled on an user, allowing plugins to influence the result by implementing <see cref="ICrossplayUserPolicy"/>
    /// </summary>
    public class CrossplayService
    {
        private readonly IEnumerable<ICrossplayUserPolicy> _policies;

        /// <summary>
        /// Creates a new <see cref="CrossplayService"/> object.
        /// </summary>
        /// <param name="policies"></param>
        public CrossplayService(IEnumerable<ICrossplayUserPolicy> policies)
        {
            this._policies = policies;
        }

        /// <summary>
        /// Returns 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool IsCrossplayEnabled(User user)
        {
            bool value = user.TryGetOption<CrossplayUserOptions>(CrossplayUserOptions.SECTION, out var option) ? option.Enabled : true;

            foreach (var policy in _policies)
            {
                value = policy.IsCrossplayEnabled(user, value);
            }

            return value;
        }
    }

    /// <summary>
    /// Contract to implement for plugin who influence if a player has cross play enabled or not.
    /// </summary>
    public interface ICrossplayUserPolicy
    {
        /// <summary>
        /// Determines if a user should be allowed to play with players on another platform.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="currentValue"></param>
        /// <returns></returns>
        bool IsCrossplayEnabled(User user, bool currentValue);
    }



}
