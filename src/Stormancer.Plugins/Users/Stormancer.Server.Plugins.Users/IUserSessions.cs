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

using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// The result of a <see cref="IUserSessions.SendRequest{TReturn, TArg}(string, string, string, TArg, CancellationToken)"/> operation.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct SendRequestResult<T>
    {
        /// <summary>
        /// Gets the value returned by the call.
        /// </summary>
        [MessagePackMember(0)]
        public T? Value { get; set; }

        /// <summary>
        /// Gets a boolean indicating whether the request was successful or resulted in an error.
        /// </summary>
        [MemberNotNullWhen(false,"Error")]
        [MessagePackMember(1)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if <see cref="Success"/> is false.
        /// </summary>
        [MessagePackMember(2)]
        public string? Error { get; set; }
    }

    /// <summary>
    /// The result of a <see cref="IUserSessions.SendRequest{TArg}(string, string, string, TArg, CancellationToken)"/> operation.
    /// </summary>
    public struct SendRequestResult
    {
        /// <summary>
        /// Gets a boolean indicating whether the request was successful or resulted in an error.
        /// </summary>
        [MemberNotNullWhen(false, "Error")]
        [MessagePackMember(0)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if <see cref="Success"/> is false.
        /// </summary>
        [MessagePackMember(1)]
        public string? Error { get; set; }
    }

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
        /// <param name="cancellationToken"></param>
        /// <returns>An user instance, or null if the peer isn't authenticated.</returns>
        Task<User?> GetUser(IScenePeerClient peer, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the peer that has been authenticated with the provided user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A peer instance of null if no peer is currently authenticated with this identity.</returns>
        Task<IScenePeerClient?> GetPeer(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Updates data associated with the user.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="peer"></param>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateUserData<T>(IScenePeerClient peer, T data, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a boolean indicating whether a peer is authenticated.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsAuthenticated(IScenePeerClient peer, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the main platform id the user id is associated with.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<PlatformId> GetPlatformId(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a session by the user id (returns null if user isn't currently connected to the scene)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Session?> GetSessionByUserId(string userId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the session associated with a peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Session?> GetSession(IScenePeerClient peer, CancellationToken cancellationToken);

        /// <summary>
        /// Gets a session by the session id of a peer.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Session?> GetSessionById(SessionId sessionId, CancellationToken cancellationToken);

        /// <summary>
        /// Get the session of a connected user through their platform-specific Id.
        /// </summary>
        /// <param name="platformId">Platform-specific Id of the user</param>
        /// <param name="cancellationToken"><c>true</c> to bypass the scene-local session cache</param>
        /// <returns>The session for <paramref name="platformId"/>. <c>null</c> if the session could not be found.</returns>
        Task<Session?> GetSession(PlatformId platformId, CancellationToken cancellationToken);

        /// <summary>
        /// Get sessions of connected users from their platform-specific Ids.
        /// </summary>
        /// <param name="platformIds">Platform-specific Ids of users to retrieve the sessions of</param>
        /// <param name="cancellationToken"></param>
        /// <returns>List of PlatformId => Session pairs. If one or more sessions could not be found, the corresponding pairs will not be present in the dictionary.</returns>
        Task<Dictionary<PlatformId, Session?>> GetSessions(IEnumerable<PlatformId> platformIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets sessions from session ids.
        /// </summary>
        /// <param name="sessionIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<SessionId, Session?>> GetSessions(IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken);

        /// <summary>
        /// Gets all sessions connected.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<Session> GetSessionsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Update a user's session data entry with raw data.
        /// </summary>
        /// <remarks>If <paramref name="key"/> is not present in the session data, it will be added.</remarks>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Session data key</param>
        /// <param name="data">Raw data to be set for <paramref name="key"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateSessionData(SessionId sessionId, string key, byte[] data, CancellationToken cancellationToken);
        /// <summary>
        /// Retrieve a user's raw session data entry.
        /// </summary>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Key of the data to be retrieved</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The raw data for <paramref name="key"/>, or <c>null</c> if <paramref name="key"/> does not exist.</returns>
        Task<byte[]?> GetSessionData(SessionId sessionId, string key, CancellationToken cancellationToken);
        /// <summary>
        /// Update a user's session data entry with an object.
        /// </summary>
        /// <remarks>The object is serialized using the server's serializer.</remarks>
        /// <typeparam name="T">Type of the object to use as session data</typeparam>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Session data key</param>
        /// <param name="data">Object to store as session data for <paramref name="key"/></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateSessionData<T>(SessionId sessionId, string key, T data, CancellationToken cancellationToken);
        /// <summary>
        /// Retrieve a user's session data entry, deserialized into a specific type.
        /// </summary>
        /// <typeparam name="T">Type of the data to be retrieved</typeparam>
        /// <param name="sessionId">Id of the session</param>
        /// <param name="key">Key of the data to be retrieved</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The deserialized object at <paramref name="key"/>, or the default value for <typeparamref name="T"/> of <paramref name="key"/> doesn't exist.</returns>
        Task<T?> GetSessionData<T>(SessionId sessionId, string key, CancellationToken cancellationToken);


        /// <summary>
        /// Queries users by user ids.
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, User?>> GetUsers(IEnumerable<string> userIds, CancellationToken cancellationToken);

        /// <summary>
        /// Queries users by fields.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="take"></param>
        /// <param name="skip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<User>> Query(IEnumerable<KeyValuePair<string, string>> query, int take, int skip, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the user handle, and optionally adds an hash number (ie name#2424) to make ids unique.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newHandle"></param>
        /// <param name="appendHash"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The new user handle.</returns>
        Task<string> UpdateUserHandle(string userId, string newHandle, bool appendHash, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a request to an user.
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="senderUserId"></param>
        /// <param name="recipientUserId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IRemotePipe SendRequest(string operationName, string senderUserId, string recipientUserId, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a request to an user.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <typeparam name="TArg"></typeparam>
        /// <param name="operationName"></param>
        /// <param name="senderUserId"></param>
        /// <param name="recipientUserId"></param>
        /// <param name="arg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a request to an user.
        /// </summary>
        /// <typeparam name="TReturn"></typeparam>
        /// <typeparam name="TArg1"></typeparam>
        /// <typeparam name="TArg2"></typeparam>
        /// <param name="operationName"></param>
        /// <param name="senderUserId"></param>
        /// <param name="recipientUserId"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<SendRequestResult<TReturn>> SendRequest<TReturn, TArg1, TArg2>(string operationName, string senderUserId, string recipientUserId, TArg1 arg1, TArg2 arg2, CancellationToken cancellationToken);


        /// <summary>
        /// Sends a request to an user.
        /// </summary>
        /// <typeparam name="TArg"></typeparam>
        /// <param name="operationName"></param>
        /// <param name="senderUserId"></param>
        /// <param name="recipientUserId"></param>
        /// <param name="arg"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<SendRequestResult> SendRequest<TArg>(string operationName, string senderUserId, string recipientUserId, TArg arg, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the number of authenticated users.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<int> GetAuthenticatedUsersCount(CancellationToken cancellationToken);

        /// <summary>
        /// Kicks an user from the server.
        /// </summary>
        /// <param name="userIds"></param>
        /// <param name="reason"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task KickUser(IEnumerable<string> userIds, string reason, CancellationToken cancellationToken);
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
            return Platform + ":" + PlatformUserId;
        }

        /// <summary>
        /// Parses a platform id string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PlatformId Parse(string value)
        {
            var els = value.Split(':');
            return new PlatformId { Platform = els[0], PlatformUserId = els[1] };
        }

        /// <summary>
        /// Platform used to authenticate the user.
        /// </summary>
        public string Platform { get; set; }

        /// <summary>
        /// Online id of the user in the platform.
        /// </summary>
        public string PlatformUserId { get; set; }

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
                return new PlatformId { Platform = "unknown", PlatformUserId = "" };
            }
        }

        /// <summary>
        /// Online id of the user in the platform.
        /// </summary>
        [Obsolete("This property is obsolete and has been renamed. Use PlatformUserId instead.", false)]
        public string OnlineId
        {
            get
            {
                return PlatformUserId;
            }
            set
            {
                PlatformUserId = value;
            }
        }
    }
}
