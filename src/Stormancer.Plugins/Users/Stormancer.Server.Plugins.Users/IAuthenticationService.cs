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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Arguments used to authenticate an user.
    /// </summary>
    [MessagePackObject]
    public class AuthParameters
    {
        /// <summary>
        /// The authentication scheme to use.
        /// </summary>
        [Key(0)]
        public required string Type { get; set; }

        /// <summary>
        /// Claims
        /// </summary>
        [Key(1)]
        public required Dictionary<string,string> Parameters { get; set; }
    }

    /// <summary>
    /// Arguments to renew an authentication token.
    /// </summary>
    [MessagePackObject]
    public class RenewCredentialsParameters
    {
        /// <summary>
        /// Updated claims.
        /// </summary>
        [Key(0)]
        public required Dictionary<string, string> Parameters { get; set; }
    }

    
    /// <summary>
    /// Authentication API.
    /// </summary>
    public interface IAuthenticationService
    {
        /// <summary>
        /// Creates an user with the provided auth parameters.
        /// </summary>
        /// <param name="auth"></param>
        /// <returns></returns>
        Task SetupAuth(AuthParameters auth);

        /// <summary>
        /// Logins an user.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="peer"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<LoginResult> Login(AuthParameters auth, IScenePeerClient peer, CancellationToken ct);
        
        /// <summary>
        /// Gets authentication metadata.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> GetMetadata();
        
        /// <summary>
        /// Gets the authentication status of a remote peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, string>> GetStatus(IScenePeerClient peer,CancellationToken cancellationToken);

        /// <summary>
        /// Unlinks an identity from an user.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        Task Unlink(User user, string type);

        /// <summary>
        /// Renew the credentials of authenticated users, if needed.
        /// </summary>
        /// <param name="threshold">How much time in advance of expiration should credentials be renewed.</param>
        /// <returns>The closest new credential expiration date.</returns>
        Task<DateTime?> RenewCredentials(TimeSpan threshold);
    }
}

