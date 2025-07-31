using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Eos
{
    /// <summary>
    /// Epic controller
    /// </summary>
    public class EosController : ControllerBase
    {
        private readonly IUserSessions _userSessions;
        private readonly ISerializer _serializer;
        private readonly IEosService _epicService;

        /// <summary>
        /// Epic controller constructor
        /// </summary>
        /// <param name="userSessions"></param>
        /// <param name="serializer"></param>
        /// <param name="epicService"></param>
        public EosController(IUserSessions userSessions, ISerializer serializer, IEosService epicService)
        {
            _userSessions = userSessions;
            _serializer = serializer;
            _epicService = epicService;
        }

        /// <summary>
        /// Get Epic accounts
        /// </summary>
        /// <param name="accountIds"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<Dictionary<string, Account>> GetAccounts(IEnumerable<string> accountIds)
        {
            return await _epicService.GetAccounts(accountIds);
        }
    }
}
