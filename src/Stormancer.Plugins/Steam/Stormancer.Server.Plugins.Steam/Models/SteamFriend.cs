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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.Server.Plugins.Steam
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Relationship between 2 steam accounts.
    /// </summary>
    public enum SteamFriendRelationship
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,
        /// <summary>
        /// The user has just clicked Ignore on an friendship invite. This doesn't get stored.
        /// </summary>
        Blocked = 1,

        /// <summary>
        /// The user has requested to be friends with the current user.
        /// </summary>
        RequestRecipient = 2,

        /// <summary>
        /// A "regular" friend.
        /// </summary>
        Friend = 3,

        /// <summary>
        /// The current user has sent a friend invite.
        /// </summary>
        RequestInitiator = 4,

        /// <summary>
        /// The current user has explicit blocked this other user from comments/chat/etc. This is stored.
        /// </summary>
        Ignored = 5,

        /// <summary>
        /// The user has ignored the current user.
        /// </summary>
        IgnoredFriend = 6,

        /// <summary>
        /// Deprecated -- Unused.
        /// </summary>
        Deprecated = 7,

    }
    /// <summary>
    /// Steam friend.
    /// </summary>
    [MessagePackObject]
    public class SteamFriend
    {
        /// <summary>
        /// Steam Id.
        /// </summary>
        [Key(0)]
        public string steamid { get; set; } = "";

        /// <summary>
        /// Relationship type.
        /// </summary>
        [Key(1)]
        public SteamFriendRelationship relationship { get; set; }

        /// <summary>
        /// Date of relation creation.
        /// </summary>
        [Key(2)]
        public ulong friend_since { get; set; }
    }

    internal class SteamFriendsList
    {
        public IEnumerable<SteamFriend>? friends { get; set; }
    }

    internal class SteamGetFriendsResponse
    {
        public SteamFriendsList? friendslist { get; set; }
    }

    [MessagePackObject]
    public class SteamGetFriendsFromClientResult
    {
        /// <summary>
        /// Was the operation successful
        /// </summary>
        [Key(0)]
        [MemberNotNullWhen(false, "ErrorId")]
        [MemberNotNullWhen(false, "ErrorDetails")]
        public bool Success { get; set; }

        /// <summary>
        /// Id of the error if the operation failed.
        /// </summary>
        [Key(1)]
        public string? ErrorId { get; set; }

        /// <summary>
        /// Details about the error if the operation failed.
        /// </summary>
        [Key(2)]
        public string? ErrorDetails { get; set; }

        [Key(3)]
        public required IEnumerable<SteamFriend> friends { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
