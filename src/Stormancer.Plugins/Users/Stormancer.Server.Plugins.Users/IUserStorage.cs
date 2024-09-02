using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides an user storage abstraction.
    /// </summary>
    public interface IUserStorage
    {
        /// <summary>
        /// Creates an use in the storage system.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task<User> CreateUser(User user);

        /// <summary>
        /// Deletes an user from the storage system.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task Delete(string id);

        /// <summary>
        /// Adds an authentication method to an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="provider"></param>
        /// <param name="identifier"></param>
        /// <param name="authDataModifier"></param>
        /// <returns></returns>
        Task<(User user, bool added)> AddAuthentication(User user, string provider, string identifier, Action<dynamic> authDataModifier);

        /// <summary>
        /// Gets an user from the storage system.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        Task<User?> GetUser(string uid);

        /// <summary>
        /// Gets several users from the storage system.
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets an user from the storage system, using on of its identities.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="identifiers"></param>
        /// <returns></returns>
        Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers);
        /// <summary>
        /// Queries users from the storage system.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken);

        /// <summary>
        /// Gets users whose user handle starts with a prefix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip);

        /// <summary>
        /// Removes an authentication identity from an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        Task<User> RemoveAuthentication(User user, string provider);

        /// <summary>
        /// Updates the last platform an user authenticated with.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="lastPlatform"></param>
        /// <returns></returns>
        Task UpdateLastPlatform(string userId, string lastPlatform);

        /// <summary>
        /// Updates user data stored with an user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uid"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task UpdateUserData<T>(string uid, T data);

        /// <summary>
        /// Updates the user handle of an user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newHandle"></param>
        /// <param name="userHandleGenerator">Optional generator method that modifies the user handle prior to save, trying to make it unique.s</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string?> UpdateUserHandleAsync(string userId, string newHandle, Func<string, string>? userHandleGenerator, CancellationToken cancellationToken);

      
    }
}
