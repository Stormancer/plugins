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
using Newtonsoft.Json.Linq;

namespace Stormancer.Server.Plugins.Users
{

    /// <summary>
    /// An user
    /// </summary>
    public class User
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public User()
        {
            Auth = new JObject();
            UserData = new JObject();
            CreatedOn = DateTime.UtcNow;
            LastLogin = DateTime.UtcNow;
            Channels = new JObject();
        }

        /// <summary>
        /// Gets or sets the id of the user.
        /// </summary>
        public string Id { get; set; } = default!;


        /// <summary>
        /// Gets or sets the auth informations of the user.
        /// </summary>
        public JObject Auth { get; set; }

        /// <summary>
        /// Gets or sets custom data about the user.
        /// </summary>
        public JObject UserData { get; set; } 

        /// <summary>
        /// Gets or sets the date the user was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the date the user last logged in.
        /// </summary>
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Gets or sets informations about the channels the user can be contacted through.
        /// </summary>
        public JObject Channels { get; set; }

        /// <summary>
        /// Stores the last platform the user authenticated on.
        /// </summary>
        public string? LastPlatform { get; set; }

        /// <summary>
        /// Gets or sets the cross platform pseudonym associated to the player, or null if it's not set
        ///
        /// </summary>
        public string? Pseudonym { get; set; }
    }

    /// <summary>
    /// An user identity.
    /// </summary>
    public class AuthenticationClaim
    {
        /// <summary>
        /// Id of the user for the provider.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Id of the user in stormancer.
        /// </summary>
        public string UserId { get; set; } = default!;

        /// <summary>
        /// Provider.
        /// </summary>
        public string Provider { get; set; } = default!;
    }
}

