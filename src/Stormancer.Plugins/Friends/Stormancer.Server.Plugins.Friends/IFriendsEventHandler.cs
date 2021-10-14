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

namespace Stormancer.Server.Plugins.Friends
{
    /// <summary>
    /// Context object passed to <see cref="IFriendsEventHandler.OnGetFriends(GetFriendsCtx)"/>.
    /// </summary>
    public class GetFriendsCtx
    {
        internal GetFriendsCtx(string userId, List<Friend> friends, bool fromServer)
        {
            UserId = userId;
            Friends = friends;
            FromServer = fromServer;
        }
        /// <summary>
        /// Gets the user id friends are requested for.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Gets the current friends list.
        /// </summary>
        public List<Friend> Friends { get; }

        /// <summary>
        /// The friends request is from the server.
        /// </summary>
        public bool FromServer { get; }
    }

    /// <summary>
    /// Friends extensibility points.
    /// </summary>
    public interface IFriendsEventHandler
    {
        /// <summary>
        /// Modifies the friend list.
        /// </summary>
        /// <param name="getFriendsCtx"></param>
        /// <returns></returns>
        Task OnGetFriends(GetFriendsCtx getFriendsCtx) => Task.CompletedTask;
    }
}
