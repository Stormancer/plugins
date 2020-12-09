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

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Authentication result.
    /// </summary>
    public class AuthenticationResult
    {
        private AuthenticationResult()
        {
        }

        /// <summary>
        /// Creates a successful <see cref="AuthenticationResult"/>.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="platformId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static AuthenticationResult CreateSuccess(User user, PlatformId platformId, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = true, AuthenticatedUser = user, PlatformId = platformId, AuthenticationContext = context };
        }

        /// <summary>
        /// Creates a failed <see cref="AuthenticationResult"/>.
        /// </summary>
        /// <param name="reason"></param>
        /// <param name="platformId"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static AuthenticationResult CreateFailure(string reason, PlatformId platformId, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = false, ReasonMsg = reason, PlatformId = platformId, AuthenticationContext = context };
        }

        /// <summary>
        /// Was authentication successful?
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the id of the User authentication associated with the session.
        /// </summary>
        public string? AuthenticatedId
        {
            get
            {
                return AuthenticatedUser?.Id;
            }
        }

        /// <summary>
        /// Gets or sets the user object authentication associated with the session.
        /// </summary>
        public User? AuthenticatedUser { get; set; }

        /// <summary>
        /// Gets or sets a custom error message.
        /// </summary>
        public string? ReasonMsg { get; set; }

        /// <summary>
        /// Gets or sets the user platform Id as returned by the authentication attempt.
        /// </summary>
        public PlatformId PlatformId { get; set; }

        /// <summary>
        /// Gets the user pseudo.
        /// </summary>
        public string Username
        {
            get
            {
                dynamic? userData = AuthenticatedUser?.UserData;
                return (string?)userData?.pseudo ?? string.Empty;
            }
        }

        /// <summary>
        /// Authentication context used to create the result.
        /// </summary>
        public Dictionary<string, string> AuthenticationContext { get; private set; } = default!;

        /// <summary>
        /// Data to initialize the session with.
        /// </summary>
        public Dictionary<string, byte[]> initialSessionData { get; } = new Dictionary<string, byte[]>();

        /// <summary>
        /// The date at which this authentication should be renewed.
        /// </summary>
        /// <remarks>
        /// Leave it to null if this authentication does not need renewing.
        /// </remarks>
        public DateTime? ExpirationDate { get; set; } = null;

        /// <summary>
        /// Callback called when the user session is created.
        /// </summary>
        public Action<SessionRecord>? OnSessionUpdated { get; set; }
    }
}

