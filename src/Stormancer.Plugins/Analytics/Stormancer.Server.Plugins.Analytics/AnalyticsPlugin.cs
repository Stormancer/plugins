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

using Stormancer.Server.Plugins.API;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.Configuration;
using System.Threading;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Components;
using System.Collections.Generic;
using Stormancer.Server.Plugins.Database;

namespace Stormancer.Server.Plugins.Analytics
{
    class AnalyticsPlugin : IHostPlugin
    {
        internal const string SCENE_ID = "analytics";
        internal const string METADATA_KEY = "stormancer.analytics";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register(static r=>new AnalyticsController(r.Resolve<IAnalyticsService>(),r.Resolve<ILogger>())).InstancePerRequest();
                  builder.Register(static r=>new AnalyticsService(r.Resolve<IEnvironment>(),r.Resolve<Lazy<IEnumerable<IAnalyticsOutput>>>())).As<IAnalyticsService>().SingleInstance();
                  builder.Register(static r=>new ApiAnalyticsEventHandler(r.Resolve<IAnalyticsService>(),r.Resolve<ConfigurationMonitor<InstrumentationConfig>>())).As<IApiHandler>().SingleInstance();
                  builder.Register(static r=>new ElasticsearchOutput(r.Resolve<IESClientFactory>(),r.Resolve<IConfiguration>(),r.Resolve<ILogger>())).As<IAnalyticsOutput>();
                  builder.Register(static r=> LocatorProvider.Instance).As<IServiceLocatorProvider>();
                  builder.Register(static r => new ConfigurationMonitor<InstrumentationConfig>(r.Resolve<IConfiguration>())).AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();
              };
            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(SCENE_ID, scene => scene.AddAnalytics());
            };
            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(SCENE_ID, SCENE_ID, false, true);
                Task.Run(async () =>
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
                    while (true)
                    {
                        try
                        {
                            await timer.WaitForNextTickAsync();
                            var analyticsService = host.DependencyResolver.Resolve<IAnalyticsService>();
                            await analyticsService.Flush();
                        }
                        catch (Exception ex)
                        {
                            host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "analytics", "failed to push analytics", ex);
                        }
                    }

                });
            };


            ctx.SceneCreated += (ISceneHost scene) =>
             {
                 if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                 {
                     scene.AddController<AnalyticsController>();
                 }
             };
        }
    }

    internal class LocatorProvider : IServiceLocatorProvider
    {

        public static LocatorProvider Instance { get; } = new LocatorProvider();
        public LocatorProvider()
        {

        }
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == AnalyticsPlugin.METADATA_KEY)
            {

                ctx.SceneId = AnalyticsPlugin.SCENE_ID;
            }
            return Task.CompletedTask;

        }
    }
}
