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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Core;

namespace Stormancer.Server.Plugins.Users
{

    /// <summary>
    /// Provids data about the current authentication attempt.
    /// </summary>
    public class AuthenticationContext
    {
        internal AuthenticationContext(Dictionary<string, string> ctx, IScenePeerClient peer, Session? currentSession)
        {
            Parameters = ctx;
            Peer = peer;
            CurrentSession = currentSession;
        }

        /// <summary>
        /// Authentication parameters sent by the client.
        /// </summary>
        public Dictionary<string, string> Parameters { get; }

        /// <summary>
        /// Peers requesting the authentication.
        /// 
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// If the user is already authenticated, gets her session
        /// </summary>
        public Session? CurrentSession { get; }
        
    }

    /// <summary>
    /// Contract for authentication providers.
    /// </summary>
    public interface IAuthenticationProvider
    {
        /// <summary>
        /// Type of the authentication provider. 
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Called when a client requests authentication capabilities.
        /// </summary>
        /// <param name="result"></param>
        void AddMetadata(Dictionary<string, string> result);

        /// <summary>
        /// Performs an authentication attempt.
        /// </summary>
        /// <param name="authenticationCtx"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<AuthenticationResult> Authenticate(AuthenticationContext authenticationCtx, CancellationToken ct);

        /// <summary>
        /// Configures the authentication system for an user.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="session">Current session if the user is currently connected.</param>
        /// <remarks>
        /// Use this function to implement create account or auth update capabilities.
        /// </remarks>
        /// <returns></returns>
        Task Setup(Dictionary<string, string> parameters, Session? session);

        /// <summary>
        /// Called when the client requests their authentication status.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        Task OnGetStatus(Dictionary<string, string> status, Session session);

        /// <summary>
        /// Unlinks an user with the authentication method implemented by the provider.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        Task Unlink(User user);

        /// <summary>
        /// This method will be called when a user's credentials should be renewed.
        /// </summary>
        /// <remarks>
        /// You only need to implement this method if your provider's credentials do expire and need renewal.
        /// In this case, you should also set <see cref="AuthenticationResult.ExpirationDate"/> in your <see cref="Authenticate(AuthenticationContext, CancellationToken)"/> implementation,
        /// as well as implement a client-side <c>renewCredentials</c> handler.
        /// </remarks>
        /// <param name="authenticationContext">
        /// Credentials renewal context for the user. 
        /// <c>authenticationContext.Parameters</c> contains provider-specific data needed for the renewal operation, sent by the client.
        /// </param>
        /// <returns>The new expiration date after the credentials have been renewed. Null if they should no longer expire.</returns>
        Task<DateTime?> RenewCredentials(AuthenticationContext authenticationContext);
    }
}
