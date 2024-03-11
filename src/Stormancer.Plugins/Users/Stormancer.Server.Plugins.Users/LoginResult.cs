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
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Result of a login operation.
    /// </summary>
    [MessagePackObject]
    public class LoginResult
    {
        /// <summary>
        /// Error message associated with the login operation, if failed.
        /// </summary>
        [Key(0)]
        public string? ErrorMsg { get; set; }

        /// <summary>
        /// A value indicating if login was successful.
        /// </summary>
        [Key(1)]
        public bool Success { get; set; }
        
        [Key(2)]
        public string? UserId { get; set; }

        [Key(3)]
        public string? Username { get; set; }

        [Key(4)]
        public Dictionary<string, string> Authentications { get; set; } = default!;

        [Key(5)]
        public Dictionary<string, string> Metadata { get; set; } = default!;
    }
}

