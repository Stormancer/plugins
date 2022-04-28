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
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Users;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Profile
{
    /// <summary>
    /// Extension methods for <see cref="IProfileService"/>.
    /// </summary>
    public static class ProfilesExtensions
    {
        /// <summary>
        /// Gets the profile of the user <paramref name="userId"/>.
        /// </summary>
        /// <param name="service"></param>
        /// <param name="userId"></param>
        /// <param name="displayOptions"></param>
        /// <param name="requestingUser"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<Dictionary<string, JObject>?> GetProfile(this IProfileService service, string userId, Dictionary<string, string> displayOptions, Session requestingUser, CancellationToken cancellationToken)
        {
            var result = await service.GetProfiles(Enumerable.Repeat(userId, 1), displayOptions, requestingUser, cancellationToken);
            if(result.TryGetValue(userId,out var r))
            {
                return r;
            }
            else
            {
                return null;
            }
        }
    }
}

