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

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Notification
{
    [ApiController]
    [Route("_notifications")]
    public class InAppNotificationAdminController : ControllerBase
    {
        
        private readonly ISceneHost scene;

        public InAppNotificationAdminController(ISceneHost scene)
        {
 
          
            this.scene = scene;
        }

        /// <summary>
        /// Send a notification
        /// </summary>
        /// <param name="notification"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("send")]
        public async Task SendNotifications(NotificationArgs notification, CancellationToken cancellationToken)
        {
            await using var scope = scene.CreateRequestScope();
            var notifications = scope.Resolve<INotificationChannel>();
            var record = new InAppNotification { 
             Acknowledgment = InAppNotificationAcknowledgment.None,
              Message = notification.Message,
               Data = notification.Data,
               UserId = notification.UserIds,
               ShouldExpire = false,
               Type = notification.Type,
              
             
            };
            await notifications.SendNotification("", record,cancellationToken);
        }
    }

    public class NotificationArgs
    {
        /// <summary>
        /// Ids of the user the notification will be sent to.
        /// </summary>
        /// <remarks>
        /// comma separated, or use * to notify all connected players.
        /// </remarks>
        public string UserIds { get; set; } = default!;

        /// <summary>
        /// Type of the notification (read in the client)
        /// </summary>
        public string Type { get; set; } = default!;

        /// <summary>
        /// Notification message.
        /// </summary>
        public string Message { get; set; } = default!;

        /// <summary>
        /// Additional data sent with the notification.
        /// </summary>
        public string Data { get; set; } = default!;
    }

    
}
