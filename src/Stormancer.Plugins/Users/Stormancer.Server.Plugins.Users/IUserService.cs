// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    public interface IUserService
    {
        Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string,string>> query, int take, int skip, CancellationToken cancellationToken);

        Task<User?> GetUser(string uid);

        Task<User> AddAuthentication(User user, string provider,string identifier, Action<dynamic> authDataModifier);

        Task<User> RemoveAuthentication(User user, string provider);

        /// <summary>
        /// Gets user by an identifier.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        Task<User?> GetUserByIdentity(string provider, string identifier);

        /// <summary>
        /// Gets users by claim (batched)
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="identifiers"></param>
        /// <returns></returns>
        Task<Dictionary<string, User?>> GetUsersByIdentity(string provider, IEnumerable<string> identifiers);

        /// <summary>
        /// Creates an user
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userData"></param>
        /// <param name="currentPlatform"></param>
        /// <returns></returns>
        Task<User> CreateUser(string userId, JObject userData, string currentPlatform);

        /// <summary>
        /// Search for users having a specific handle prefix.
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        Task<IEnumerable<User>> QueryUserHandlePrefix(string prefix, int take, int skip);

        /// <summary>
        /// Updates the current user platform.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="lastPlatform"></param>
        /// <returns></returns>
        Task UpdateLastPlatform(string uid, string lastPlatform);

        /// <summary>
        /// Updates the data of an user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="uid"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task UpdateUserData<T>(string uid, T data);



        /// <summary>
        /// Deletes an user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task Delete(string id);

        /// <summary>
        /// Updates the handle of a player.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newHandle"> </param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new handle, including an unicity hash at the end if configured to generate one.</returns>
        Task<string?> UpdateUserHandleAsync(string userId,string newHandle,CancellationToken cancellationToken);

        Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken);
    }
}
