using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Objects implementing this interface can customize the <see cref="Limits"/> plugin.
    /// </summary>
    public interface ILimitsEventHandler
    {
        /// <summary>
        /// Called when the plugin will enforce a max CCU limit.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnApplyingUserLimit(OnApplyingUserLimitContext ctx) => Task.CompletedTask;
    }

    /// <summary>
    /// Context passed to <see cref="ILimitsEventHandler.OnApplyingUserLimit(OnApplyingUserLimitContext)"/>.
    /// </summary>
    public class OnApplyingUserLimitContext
    {
        internal OnApplyingUserLimitContext(AuthenticationCompleteContext result)
        {
            AuthResult = result;
        }

        /// <summary>
        /// Authentication result.
        /// </summary>
        public AuthenticationCompleteContext AuthResult { get; }

        /// <summary>
        /// Set to false to skip limit for the auth request.
        /// </summary>
        public bool ApplyUserLimit { get; set; } = true;
    }
}
