using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    public interface IUserStorage
    {
        Task<User> CreateUser(User user);
        Task Delete(string id);
        Task<(User user, bool added)> GetAuthentication(User user, string provider, string identifier, Action<dynamic> authDataModifier);
        Task<User?> GetUser(string uid);
        Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken);
        Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers);
        Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken);
        Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip);
        Task<User> RemoveAuthentication(User user, string provider);
        Task UpdateLastPlatform(string userId, string lastPlatform);
        Task UpdateUserData<T>(string uid, T data);
        Task<string?> UpdateUserHandleAsync(string userId, string newHandle, Func<string, string>? userHandleGenerator, CancellationToken cancellationToken);

      
    }
}
