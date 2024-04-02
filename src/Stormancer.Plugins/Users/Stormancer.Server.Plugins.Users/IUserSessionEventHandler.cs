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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Context passed to <see cref="IUserSessionEventHandler.OnLoggedIn(LoginContext)"/>
    /// </summary>
    public class LoginContext
    {
        /// <summary>
        /// Gets the user being logged in.
        /// </summary>
        public required User? AuthenticatedUser { get; init; }
        /// <summary>
        /// Gets the session being logged in.
        /// </summary>
        public required SessionRecord Session { get; init; }

        /// <summary>
        /// Gets the peer which triggered the log in event.
        /// </summary>
        public required IScenePeerClient Client { get; init; }


        /// <summary>
        /// Gets the already connected sessions associated with the user being logged in.
        /// </summary>
        public  required IEnumerable<Session> ConnectedSessions { get; init; }
    }

    public class LogoutContext
    {
        public Session Session { get; set; }
        public DateTime ConnectedOn { get; set; }
        public DisconnectionReason Reason { get; internal set; }
    }

    public class UpdateUserHandleCtx
    {
        public UpdateUserHandleCtx(string userId, string newHandle)
        {
            UserId = userId;
            NewHandle = newHandle;
        }

        public string UserId { get; }
        public string NewHandle { get; }
        public bool Accept { get; set; } = true;
        public string ErrorMessage { get; set; } = "";
    }
    /// <summary>
    /// Context passed to <see cref="IUserSessionEventHandler.OnKicking(KickContext)"/> event handler.
    /// </summary>
    public class KickContext
    {
        internal KickContext(IScenePeerClient peer, Session? session,IEnumerable<string> query)
        {
            Peer = peer;
            Session = session;
            Query = query;
        }

        /// <summary>
        /// Peer examined
        /// </summary>
        public IScenePeerClient Peer { get; }

        /// <summary>
        /// Session of the player examined.
        /// </summary>
        public Session? Session { get; }

        /// <summary>
        /// Kick query.
        /// </summary>
        public IEnumerable<string> Query { get; }

        /// <summary>
        /// A boolean indicating whether the peer should be kicked.
        /// </summary>
        public bool Kick { get; set; } = true;
    }

    public interface IUserSessionEventHandler
    {
        Task OnLoggedIn(LoginContext ctx)=>Task.CompletedTask;

        Task OnLoggedOut(LogoutContext ctx) => Task.CompletedTask;

       

        /// <summary>
        /// Called for each peer when a * kick request is performed.
        /// </summary>
        /// <param name="ctx"></param>
        Task OnKicking(KickContext ctx) => Task.CompletedTask;
    }
}

