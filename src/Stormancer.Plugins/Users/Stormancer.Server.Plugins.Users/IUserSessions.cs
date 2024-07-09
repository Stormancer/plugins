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

using MessagePack;
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
    [MessagePackObject]
    public struct SendRequestResult<T>
    {
        /// <summary>
        /// Gets the value returned by the call.
        /// </summary>
        [Key(0)]
        public T? Value { get; set; }

        /// <summary>
        /// Gets a boolean indicating whether the request was successful or resulted in an error.
        /// </summary>
        [MemberNotNullWhen(false,"Error")]
        [Key(1)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if <see cref="Success"/> is false.
        /// </summary>
        [Key(2)]
        public string? Error { get; set; }
    }

    /// <summary>
    /// The result of a <see cref="IUserSessions.SendRequest{TArg}(string, string, string, TArg, CancellationToken)"/> operation.
    /// </summary>
    [MessagePackObject]
    public struct SendRequestResult
    {
        /// <summary>
        /// Gets a boolean indicating whether the request was successful or resulted in an error.
        /// </summary>
        [MemberNotNullWhen(false, "Error")]
        [Key(0)]
        public bool Success { get; set; }

        /// <summary>
        /// Gets the error message if <see cref="Success"/> is false.
        /// </summary>
        [Key(1)]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Provides APIs to interact with client sessions.
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
        Task<IEnumerable<SessionId>> GetPeers(string userId, CancellationToken cancellationToken);

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
        /// Gets a session by the user id (returns null if user isn't currently connected to the scene)
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<Session>> GetSessionsByUserId(string userId, CancellationToken cancellationToken);

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

        /// <summary>
        /// Updates the user options.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateUserOptionsAsync(string userId, string key, JObject value, CancellationToken cancellationToken);


        /// <summary>
        /// Gets information about an user.
        /// </summary>
        /// <param name="platform"></param>
        /// <param name="ids"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, UserSessionInfos>> GetDetailedUserInformationsByIdentityAsync(string platform, IEnumerable<string> ids, CancellationToken cancellationToken);
    }


    /// <summary>
    /// Contains informations about an user.
    /// </summary>
    /// <remarks>
    /// Contains the Session if the player is connected, the User if they are not connected, but a persistent user was found in the database, or nothing if the user couldn't be found.
    /// </remarks>
    [MessagePackObject]
    public class UserSessionInfos
    {
        [Key(0)]
        public User? User { get; set; }

        [Key(1)]
        public IEnumerable<Session> Sessions { get; set; }
    }

}
