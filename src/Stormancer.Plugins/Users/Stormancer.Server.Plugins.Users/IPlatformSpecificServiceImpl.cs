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

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides a way for platforms to provide display names for a specific user id.
    /// </summary>
    public interface IPlatformSpecificServiceImpl
    {
        /// <summary>
        /// Gets the string identifying the platform.
        /// </summary>
        string Platform { get; }

        /// <summary>
        /// Gets the display name for an user.
        /// </summary>
        /// <param name="platformId"></param>
        /// <returns></returns>
        Task<string?> GetDisplayableUserId(PlatformId platformId);

        /// <summary>
        /// Gets display names for a list of users.
        /// </summary>
        /// <param name="platformIds"></param>
        /// <returns></returns>
        Task<Dictionary<PlatformId, string?>> GetDisplayableUserIds(IEnumerable<PlatformId> platformIds);
    }
}
