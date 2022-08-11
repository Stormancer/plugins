using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Epic Platform service
    /// </summary>
    public interface IEpicService
    {
        /// <summary>
        /// Is Epic the main auth of this session?
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool IsEpicAccount(Session session);

        /// <summary>
        /// Get Epic accounts.
        /// </summary>
        /// <param name="accountIds"></param>
        /// <returns></returns>
        public Task<Dictionary<string, Account>> GetAccounts(IEnumerable<string> accountIds);
    }
}
