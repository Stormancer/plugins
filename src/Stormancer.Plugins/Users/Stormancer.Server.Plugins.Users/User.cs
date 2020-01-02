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
    public class User
    {
        public User()
        {
            Auth = new JObject();
            UserData = new JObject();
            CreatedOn = DateTime.UtcNow;
            LastLogin = DateTime.UtcNow;
            Channels = new JObject();
        }

        public string Id { get; set; }

    
        public JObject Auth { get; set; }
        public JObject UserData { get; set; } 

        public DateTime CreatedOn { get; set; }

        public DateTime LastLogin { get; set; }

        public JObject Channels { get; set; }
    }

    public class AuthenticationClaim
    {
        public string Id { get; set; }

        public string UserId { get; set; }

        public string Provider { get; set; }
    }
}

