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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Stormancer;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides extensibility to user related operations
    /// </summary>
    public interface IUserEventHandler
    {
        /// <summary>
        /// Event triggered whenever the authentication methods of an user is updated.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnAuthenticationChanged(AuthenticationChangedCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Event triggered whenever an user handle is updated.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        /// <remarks>User handles provide display names stored directly in the user.</remarks>
        Task OnUpdatingUserHandle(UpdateUserHandleCtx ctx)
        {
            return Task.CompletedTask;
        }

      
    }

    /// <summary>
    /// Implement this interface to extract information used to identify a player on the database from one of their <see cref="PlatformId"/>
    /// </summary>
    public interface IUserIdentityIdProvider
    {
        /// <summary>
        /// Gets a boolean indicating if the provider can handle a platform.
        /// </summary>
        /// <param name="platform"></param>
        /// <returns></returns>
        bool CanHandle(string platform);

        /// <summary>
        /// Gets the identity information to use when querying the DB for the user.
        /// </summary>
        /// <param name="platformId"></param>
        /// <returns></returns>
        Task<string> GetIdentityInformation(PlatformId platformId);
    }

    /// <summary>
    /// Context passed to <see cref="IUserEventHandler.OnAuthenticationChanged(AuthenticationChangedCtx)"/>.
    /// </summary>
    public class AuthenticationChangedCtx
    {
        /// <summary>
        /// Type of authentication update.
        /// </summary>
        public enum AuthenticationUpdateType
        {

            /// <summary>
            /// No storage system configured on the server.
            /// </summary>
            None,

            /// <summary>
            /// User linked to an authentication provider.
            /// </summary>
            Add,

            /// <summary>
            /// User unlinked from an authentication provider.
            /// </summary>
            Remove,

            /// <summary>
            /// Update the configuration of the authentication provider for the user.
            /// </summary>
            Update,
        }

        internal AuthenticationChangedCtx(AuthenticationUpdateType updateType, string type, User user)
        {
            UpdateType = updateType;
            Provider = type;
            User = user;
        }

        /// <summary>
        /// Gets the change type.
        /// </summary>
        public AuthenticationUpdateType UpdateType { get; }

        /// <summary>
        /// Gets the id of the provider.
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Gets the user the change was applied to.
        /// </summary>
        public User User { get; }
    }

    /// <summary>
    /// Context class passed to <see cref="IUserEventHandler.OnUpdatingUserHandle(UpdateUserHandleCtx)"/>
    /// </summary>
    public class UpdateUserHandleCtx
    {
        internal UpdateUserHandleCtx(string userId, string newHandle)
        {
            UserId = userId;
            NewHandle = newHandle;
        }

        /// <summary>
        /// Gets the id of the user.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Gets the new handle.
        /// </summary>
        public string NewHandle { get; }

        /// <summary>
        /// Gets or sets a boolean indicating if the new handle should be accepted.
        /// </summary>
        public bool Accept { get; set; } = true;

        /// <summary>
        /// Gets or sets the error message if the update was rejected.
        /// </summary>
        public string ErrorMessage { get; set; } = "";
    }
}

