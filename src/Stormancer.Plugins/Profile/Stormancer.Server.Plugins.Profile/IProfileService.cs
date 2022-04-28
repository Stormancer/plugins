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
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    /// <summary>
    /// Provides methods to access profile informations about a player.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Gets user profiles.
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="displayOptions"></param>
        /// <param name="requestingUser"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string,Dictionary<string,JObject>?>> GetProfiles(IEnumerable<string> userIds, Dictionary<string, string> displayOptions, Session? requestingUser, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the user handle and returns the new handle.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newHandle"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<string> UpdateUserHandle(string userId, string newHandle, CancellationToken cancellationToken);

        /// <summary>
        /// Updates a custom profile part associated with an user.
        /// </summary>
        /// <remarks>Custom profile parts can be updated by the client.</remarks>
        /// <param name="userId"></param>
        /// <param name="partId"></param>
        /// <param name="version"></param>
        /// <param name="fromClient"></param>
        /// <param name="inputStream"></param>
        /// <returns></returns>
        Task UpdateCustomProfilePart(string userId,string partId, string version, bool fromClient, Stream inputStream);

        /// <summary>
        /// Deletes a custom profile part.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="partId"></param>
        /// <param name="fromClient"></param>
        /// <returns></returns>
        Task DeleteCustomProfilePart(string id, string partId, bool fromClient);
    }
}
