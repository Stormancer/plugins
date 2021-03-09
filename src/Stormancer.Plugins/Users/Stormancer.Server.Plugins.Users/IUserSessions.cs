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
using Stormancer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides APIs to incteract with client sessions.
    /// </summary>
    /// <remarks>
    /// 
    /// </remarks>
    public interface IUserSessions
    {
        /// <summary>
        /// Gets the identity of a connected peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns>An user instance, or null if the peer isn't authenticated.</returns>
        Task<User?> GetUser(IScenePeerClient peer);

        /// <summary>
        /// Gets the peer that has been authenticated with the provided user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>A peer instance of null if no peer is currently authenticated with this identity.</returns>
        Task<IScenePeerClient?> GetPeer(string userId);

        /// <summary>
        /// Updates data associated with the user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="peer"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task UpdateUserData<T>(IScenePeerClient peer, T data);

        /// <summary>
        /// Gets a boolean indicating whether a peer is authenticated.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task<bool> IsAuthenticated(IScenePeerClient peer);

        /// <summary>
        /// Gets the main platform id the user id is associated with.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<PlatformId> GetPlatformId(string userId);

        /// <summary>
        /// Gets a session by the user id (returns null if user isn't currently connected to the scene)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        Task<Session?> GetSessionByUserId(string userId, bool forceRefresh = false);

        /// <summary>
        /// Gets the session associated with a peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        Task<Session?> GetSession(IScenePeerClient peer, bool forceRefresh = false);

        /// <summary>
        /// Gets a session by the session id of a peer.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        Task<Session?> GetSessionById(string sessionId, bool forceRefresh = false);

        /// <summary>
        /// Gets a session by the session id of a peer.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="authType"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        Task<Session?> GetSessionById(string sessionId, string authType, bool forceRefresh = false);

        /// <summary>
        /// Get the session of a connected user through their platform-specific Id.
        /// </summary>
        /// <param name="platformId">Platform-specific Id of the user</param>
        /// <param name="forceRefresh"><c>true</c> to bypass the scene-local session cache</param>
        /// <returns>The session for <paramref name="platformId"/>. <c>null</c> if the session could not be found.</returns>
        Task<Session?> GetSession(PlatformId platformId, bool forceRefresh = false);

        /// <summary>
        /// Get sessions of connected users from their platform-specific Ids.
        /// </summary>
        /// <param name="platformIds">Platform-specific Ids of users to retrieve the sessions of</param>
        /// <param name="forceRefresh"><c>true</c> to bypass the scene-local session cache</param>
        /// <returns>List of PlatformId => Session pairs. If one or more sessions could not be found, the corresponding pairs will not be present in the dictionary.</returns>
        Task<Dictionary<PlatformId, Session?>> GetSessions(IEnumerable<PlatformId> platformIds, bool forceRefresh = false);

        /// <summary>
        /// Gets sessions from session ids.
        /// </summary>
        /// <param name="sessionIds"></param>
        /// <param name="forceRefresh"></param>
        /// <returns></returns>
        Task<Dictionary<string, Session?>> GetSessions(IEnumerable<string> sessionIds, bool forceRefresh = false);


        /// <summary>
        /// Update a user's session data entry with raw data.
        /// </summary>
        /// <remarks>If <paramref name="key"/> is not present in the session data, it will be added.</remarks>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Session data key</param>
        /// <param name="data">Raw data to be set for <paramref name="key"/></param>
        /// <returns></returns>
        Task UpdateSessionData(string sessionId, string key, byte[] data);
        /// <summary>
        /// Retrieve a user's raw session data entry.
        /// </summary>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Key of the data to be retrieved</param>
        /// <returns>The raw data for <paramref name="key"/>, or <c>null</c> if <paramref name="key"/> does not exist.</returns>
        Task<byte[]?> GetSessionData(string sessionId, string key);
        /// <summary>
        /// Update a user's session data entry with an object.
        /// </summary>
        /// <remarks>The object is serialized using the server's serializer.</remarks>
        /// <typeparam name="T">Type of the object to use as session data</typeparam>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Session data key</param>
        /// <param name="data">Object to store as session data for <paramref name="key"/></param>
        /// <returns></returns>
        Task UpdateSessionData<T>(string sessionId, string key, T data);
        /// <summary>
        /// Retrieve a user's session data entry, deserialized into a specific type.
        /// </summary>
        /// <typeparam name="T">Type of the data to be retrieved</typeparam>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Key of the data to be retrieved</param>
        /// <returns>The deserialized object at <paramref name="key"/>, or the default value for <typeparamref name="T"/> of <paramref name="key"/> doesn't exist.</returns>
        Task<T> GetSessionData<T>(string sessionId, string key);


        /// <summary>
        /// Queries users by user ids.
        /// </summary>
        /// <param name="userIds"></param>
        /// <returns></returns>
        Task<Dictionary<string, User?>> GetUsers(params string[] userIds);

        /// <summary>
        /// Queries users by fields.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string,string>> query, int take, int skip);

        /// <summary>
        /// Updates the user handle, and optionally adds an hash number (ie name#2424) to make ids unique.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newHandle"></param>
        /// <param name="appendHash"></param>
        /// <returns></returns>
        Task UpdateUserHandle(string userId, string newHandle, bool appendHash);

        /// <summary>
        /// Sends a request to an user.
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="senderUserId"></param>
        /// <param name="recipientUserId"></param>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IObservable<byte[]> SendRequest(string operationName, string senderUserId, string recipientUserId, Action<Stream> writer, CancellationToken cancellationToken);


        /// <summary>
        /// Gets the number of authenticated users.
        /// </summary>
        /// <returns></returns>
        Task<int> GetAuthenticatedUsersCount();

        /// <summary>
        /// Kicks an user from the server.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        Task KickUser(string userId, string reason);
    }

   

    /// <summary>
    /// Represents the Id of an user in a platform.
    /// </summary>
    /// <remarks>
    /// An user may be authenticated to several platforms, in this case he will be associated to a main <see cref="PlatformId"/> and secondary ones.
    /// </remarks>
    public struct PlatformId
    {

        /// <summary>
        /// Gets a string rep
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Platform + ":" + OnlineId;
        }

        /// <summary>
        /// Parses a platform id string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PlatformId Parse(string value)
        {
            var els = value.Split(':');
            return new PlatformId { Platform = els[0], OnlineId = els[1] };
        }

        /// <summary>
        /// Platform used to authenticate the user.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Online id of the user in the platform.
        /// </summary>
        public string OnlineId { get; set; }

        /// <summary>
        /// Returns true if the platform id is "unknown".
        /// </summary>
        public bool IsUnknown
        {
            get
            {
                return Platform == "unknown";
            }
        }

        /// <summary>
        /// Represents an Unknow platform Id.
        /// </summary>
        public static PlatformId Unknown
        {
            get
            {
                return new PlatformId { Platform = "unknown", OnlineId = "" };
            }
        }


    }
}

