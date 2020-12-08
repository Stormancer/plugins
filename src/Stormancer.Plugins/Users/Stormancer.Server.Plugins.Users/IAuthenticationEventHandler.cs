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

using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Objects implementing this interface can participate in the authentication process.
    /// </summary>
    public interface IAuthenticationEventHandler
    {
        /// <summary>
        /// Called when an authentication request has been received.
        /// </summary>
        /// <param name="authenticationCtx"></param>
        /// <returns></returns>
        Task OnLoggingIn(LoggingInCtx authenticationCtx) => Task.CompletedTask;

        /// <summary>
        /// Called when authentication is complete but the session is not created yet.
        /// </summary>
        /// <remarks>
        /// Both successful and rejected authentication requests trigger the event.
        /// The handler allows changing the result of the authentication test, for instance to refuse an accepted authentication.
        /// If no authentication provider was able to server the authentication request, this method is not called.
        /// </remarks>
        /// <param name="ctx"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task OnAuthenticationComplete(AuthenticationCompleteContext ctx, CancellationToken cancellationToken) => Task.CompletedTask;
        /// <summary>
        /// Called when authentication is complete and a session was created for the user.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnLoggedIn(LoggedInCtx ctx) => Task.CompletedTask;
    }

    /// <summary>
    /// Context passed to <see cref="IAuthenticationEventHandler.OnAuthenticationComplete(AuthenticationCompleteContext, CancellationToken)"/>
    /// </summary>
    public class AuthenticationCompleteContext
    {
        internal AuthenticationCompleteContext(AuthenticationResult result, IScenePeerClient peer, SessionRecord? session)
        {
            Result = result;
            Peer = peer;
            CurrentSession = session;
        }
        /// <summary>
        /// Result of the authentication phase.
        /// </summary>
        public AuthenticationResult Result { get;}

        /// <summary>
        /// Peer requesting authentication.
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// User session if they were already connected.
        /// </summary>
        public SessionRecord? CurrentSession { get; }
    }

    /// <summary>
    /// Context passed to <see cref="IAuthenticationEventHandler.OnLoggingIn(LoggingInCtx)"/>
    /// </summary>
    public class LoggingInCtx
    {
        public AuthenticationContext AuthCtx { get; set; }
        public bool HasError { get; set; }
        public string Reason { get; set; }
        public string Type { get; internal set; }
    }


   
    /// <summary>
    /// Context passed to <see cref="IAuthenticationEventHandler.OnLoggedIn(LoggedInCtx)"/>
    /// </summary>
    public class LoggedInCtx
    {
        public AuthenticationContext AuthCtx { get; set; }
        public LoginResult Result { get; set; }
        public SessionRecord Session { get; set; }
    }
}
