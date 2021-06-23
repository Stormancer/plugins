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

using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Notification
{
    class NotificationPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.inappnotification";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneCreating += (ISceneHost scene) =>
              {
                  if(scene.Template == Users.Constants.SCENE_TEMPLATE)
                  {
                      scene.Metadata[METADATA_KEY] = "true";
                  }
              };
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<InAppNotificationAdminController>();
                builder.Register<AdminWebApiConfig>().As<IAdminWebApiConfig>();
                builder.Register<InAppNotificationRepository>();
                builder.Register<ProxyNotificationChannel>().As<INotificationChannel>().InstancePerDependency();
                builder.Register<NotificationChannelController>();
                builder.Register<InAppNotificationProvider>().As<INotificationProvider>().AsSelf().InstancePerScene();
                builder.Register<NotificationLocator>().As<IServiceLocatorProvider>();
            };

            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    builder.Register<NotificationChannel>(dr => new NotificationChannel(dr.Resolve<Func<IEnumerable<INotificationProvider>>>())).As<INotificationChannel>().InstancePerDependency();
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<NotificationChannelController>();
                    // Instantiate InAppNotificationProvider singleton to subscribe to scene events
                    scene.DependencyResolver.Resolve<InAppNotificationProvider>();
                }
            };
        }
    }


    class NotificationLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            switch (ctx.ServiceType)
            {
                case "stormancer.plugins.notifications":
                    ctx.SceneId = Users.Constants.GetSceneId();
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
