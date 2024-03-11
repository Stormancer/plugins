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

namespace Stormancer.Server.Plugins.Notification
{
    /// <summary>
    /// Types of notification acknowledgment.
    /// </summary>
    public enum InAppNotificationAcknowledgment : byte
    {
        /// <summary>
        /// No acknowledgment. The notification is not stored in the database if it couldn't be sent.
        /// </summary>
        None = 0,

        /// <summary>
        /// The notification is considered as acknowledged when it is sent to a client logged as the target user.
        /// </summary>
        OnSend = 1,

        /// <summary>
        /// The notification is acknowledged  when the game code handled it.
        /// </summary>
        /// <remarks>
        /// If no handler was configured for the notification system in the game client, the notification will be considered as not acknowledged.
        /// This enables running different game clients with only some of them supporting notifications.
        /// </remarks>
        OnReceive = 2,

        /// <summary>
        /// The notification is acknowledged when the `acknowledge` function is called on the client.
        /// </summary>
        ByUser = 3
    }


    /// <summary>
    /// A notification stored in the database.
    /// </summary>
    public class InAppNotificationRecord
    {
        /// <summary>
        /// Creates an <see cref="InAppNotificationRecord"/> object.
        /// </summary>
        public InAppNotificationRecord()
        {
        }

        /// <summary>
        /// Creates an <see cref="InAppNotificationRecord"/> object.
        /// </summary>
        /// <param name="data"></param>
        public InAppNotificationRecord(InAppNotification data)
        {
            Id = data.Id;
            Type = data.Type;
            UserId = data.UserId;
            Message = data.Message;
            Data = data.Data;
            CreatedOn = data.CreatedOn;
            ShouldExpire = data.ShouldExpire;
            ExpirationDate = data.ExpirationDate;
            Acknowledgment = data.Acknowledgment;
          
        }

        /// <summary>
        /// Gets or sets the id of the notification.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of the notification.
        /// </summary>
        public string Type { get; set; } = default!;

        /// <summary>
        /// Gets or sets the id of the user the notification is addressed to.
        /// </summary>
        public string UserId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the  message of the notification.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Gets or sets custom data associated with the notification.
        /// </summary>
        public string Data { get; set; } = default!;

        /// <summary>
        /// Gets or sets the date the notification was created.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if the notification expires.
        /// </summary>
        public bool ShouldExpire { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the notification.
        /// </summary>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        /// Gets or sets the type of acknowledgment this notification expects to be considered as sent.
        /// </summary>
        public InAppNotificationAcknowledgment Acknowledgment { get; set; } = InAppNotificationAcknowledgment.None;
        

    }

    /// <summary>
    /// A notification sent to the game client.
    /// </summary>
    [MessagePackObject]
    public class InAppNotification
    {
        /// <summary>
        /// Creates an <see cref="InAppNotification"/> object.
        /// </summary>
        public InAppNotification()
        {
        }

        /// <summary>
        /// Creates an <see cref="InAppNotification"/> object;
        /// </summary>
        /// <param name="record"></param>
        public InAppNotification(InAppNotificationRecord record)
        {
            Id = record.Id;
            Type = record.Type;
            UserId = record.UserId;
            Message = record.Message;
            Data = record.Data;
            CreatedOn = record.CreatedOn;
            ShouldExpire = record.ShouldExpire;
            ExpirationDate = record.ExpirationDate;
            Acknowledgment = record.Acknowledgment;
           
        }

        /// <summary>
        /// Gets or sets the id of the notification.
        /// </summary>
        [Key(0)]
        public string Id { get; set; } = default!;

        /// <summary>
        /// Gets or sets the type of the notification.
        /// </summary>
        [Key(1)]
        public string Type { get; set; } = default!;

        /// <summary>
        /// Gets or sets the id of the user the notification is addressed to.
        /// </summary>
        [Key(2)]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the  message of the notification.
        /// </summary>
        [Key(3)]
        public string Message { get; set; } = default!;

        /// <summary>
        /// Gets or sets custom data associated with the notification.
        /// </summary>
        [Key(4)]
        public string Data { get; set; } = default!;

        /// <summary>
        /// Gets or sets the date the notification was created.
        /// </summary>
        [Key(5)]
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="bool"/> indicating if the notification expires.
        /// </summary>
        [Key(6)]
        public bool ShouldExpire { get; set; }

        /// <summary>
        /// Gets or sets the expiration date of the notification.
        /// </summary>
        [Key(7)]
        public DateTime ExpirationDate { get; set; }
  
        /// <summary>
        /// Gets or sets the type of acknowledgment this notification expects to be considered as sent.
        /// </summary>
        [Key(8)]
        public InAppNotificationAcknowledgment Acknowledgment { get; set; }

      
    }
}
