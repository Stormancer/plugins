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

namespace Stormancer.Server.Plugins.Friends
{
   /// <summary>
   /// Type of operation on a client friend list.
   /// </summary>
    public enum FriendListUpdateDtoOperation
    {
        /// <summary>
        /// Adds or update a record.
        /// </summary>
        AddOrUpdate = 0,

        /// <summary>
        /// Removes a record.
        /// </summary>
        Remove = 1,

        /// <summary>
        /// Updates the status of a record.
        /// </summary>
        UpdateStatus = 2,

     
    }

    /// <summary>
    /// Dto sent to client friend lists to update them.
    /// </summary>
    [MessagePackObject]
    public class FriendListUpdateDto
    {
       
        /// <summary>
        /// Gets or sets the Operation to be performed on the friend list.
        /// </summary>
        [Key(0)]
        public required FriendListUpdateDtoOperation Operation { get; set; }

        /// <summary>
        /// Gets or sets the content of the update.
        /// </summary>
        [Key(1)]
        public required Friend Data { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the message.
        /// </summary>
        /// <remarks>currently unused both by server and client</remarks>
        [Key(2)]
        public ulong Timestamp { get; set; } =(ulong)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
    }
}
