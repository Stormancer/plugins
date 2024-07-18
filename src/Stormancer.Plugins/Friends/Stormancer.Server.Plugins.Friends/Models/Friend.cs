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
using Stormancer.Server.Plugins.Friends.Data;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Friends
{
    [MessagePackObject]
    public class MemberDto
    {
        public MemberDto()
        {
        }

        public MemberDto(MemberRecord record)
        {
            FriendId = record.FriendId.ToString();
            OwnerId = record.OwnerId.ToString();
            Status = record.Status;
            Tags = record.Tags;
            Expiration = record.Expiration ?? DateTime.MaxValue;
        }

        [Key(0)]
        public string Id
        {
            get
            {
                return OwnerId + "_" + FriendId;
            }
        }

        [Key(1)]
        public string FriendId { get; set; }

        [Key(2)]
        public string OwnerId { get; set; }

        [Key(3)]
        public MemberRecordStatus Status { get; set; }

        [Key(4)]
        public List<string> Tags { get; set; } = new List<string>();


        /// <summary>
        /// Expiration of the record. Only applicable to block records.
        /// </summary>
        [Key(5)]
        public DateTime Expiration { get; set; } = default;
    }



    /// <summary>
    /// Friend object in the client.
    /// </summary>
    [MessagePackObject]
    public class Friend
    {
        /// <summary>
        /// Gets or sets the list of user ids associated with the friend entry.
        /// </summary>
        /// <remarks>User ids include Stormancer user ids, Steam ids, etc...</remarks>
        [Key(0)]
        public required List<PlatformId> UserIds { get; set; }

        /// <summary>
        /// Gets or sets the connection status of the friend.
        /// </summary>
        [Key(1)]
        public required FriendConnectionStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the tags associated with the friend.
        /// </summary>
        [Key(2)]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Gets or sets a json string representing custom data associated with the friend.
        /// </summary>
        [Key(3)]
        public string? CustomData;

    }



    /// <summary>
    /// Status of a member record.
    /// </summary>
    public enum MemberRecordStatus
    {
        /// <summary>
        /// An accepted friend association
        /// </summary>
        Accepted = 0,

        /// <summary>
        /// The record is a sent invitation.
        /// </summary>
        SentInvitation = 1,

        /// <summary>
        /// An invitation pending approval
        /// </summary>
        PendingInvitation = 2,

        /// <summary>
        /// The association has been deleted or is blocked by the remote user.
        /// </summary>
        DeletedByFriend = 3,

        /// <summary>
        /// The association is blocked by the current user.
        /// </summary>
        Blocked = 4
    }

   /// <summary>
   /// Player visibility status configuration.
   /// </summary>
    public enum FriendListStatusConfig
    {
        /// <summary>
        /// Player is displayed online if they are.
        /// </summary>
        Online = 0,
        /// <summary>
        /// Player always invisible.
        /// </summary>
        Invisible = 1,

        /// <summary>
        /// Player displayed as away instead of online when connected.
        /// </summary>
        Away = 2
    }

    /// <summary>
    /// Connection status of a friend.
    /// </summary>
    public enum FriendConnectionStatus
    {
        /// <summary>
        /// The user is in game.
        /// </summary>
        Connected = 3,

        /// <summary>
        /// The user is online on the platform, but not in game.
        /// </summary>
        Online = 2,

        /// <summary>
        /// Player away
        /// </summary>
        Away = 1,

        /// <summary>
        /// Player disconnected.
        /// </summary>
        Disconnected = 0
    }
}
