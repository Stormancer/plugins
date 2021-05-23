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
using Stormancer.Server.Plugins.API;
using Stormancer;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Reactive.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;

namespace Stormancer.Server.Plugins.Notification
{
    [Service(ServiceType = "stormancer.plugins.notifications")]
    class NotificationChannelController : ControllerBase
    {
        private readonly INotificationChannel channel;

        public NotificationChannelController(INotificationChannel channel)
        {
            this.channel = channel;
        }

        [S2SApi]
        public Task<bool> SendNotification(string type, InAppNotification data, CancellationToken cancellationToken)
        {
            return channel.SendNotification(type, data, cancellationToken);
        }
    }

    class ProxyNotificationChannel : INotificationChannel
    {
        private readonly NotificationChannelProxy proxy;

        public ProxyNotificationChannel(NotificationChannelProxy proxy)
        {
            this.proxy = proxy;
        }

        public Task<bool> SendNotification(string type, dynamic data, CancellationToken cancellationToken)
        {
            var notif = data as InAppNotification;

            if (notif == null)
            {
                return Task.FromResult(false);
            }

            return proxy.SendNotification(type, notif, cancellationToken);

            
        }
    }
}
