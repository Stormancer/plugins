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

using Stormancer.Abstractions.Server.Components;
using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    class ServiceLocatorPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += (IHost host) =>
            {
                host.RegisterAppFunction("ServiceLocator.Query", async (IAppFunctionContext ctx) => {

                    var host = ctx.Resolver.Resolve<ServiceLocatorHostDatabase>();
                    var serializer = ctx.Resolver.Resolve<IClusterSerializer>();
                    var serviceType = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);
                    var instanceId = await serializer.DeserializeAsync<string>(ctx.Input, CancellationToken.None);

                    host.TryGetScene(serviceType, instanceId, out var scene);
                    serializer.Serialize(ctx.Output,scene?.Id);
                    
                });
            };
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
              {
                  if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                  {
                      builder.Register<LocatorController>(r=>new LocatorController(
                          r.Resolve<IServiceLocator>(),
                          r.Resolve<IUserSessions>(),
                          r.Resolve<ILogger>())
                      ).InstancePerRequest();


                  }
              };
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register(static r => new ServiceLocatorConfiguration(r.Resolve<IConfiguration>())).AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();
                  builder.Register(static r => new ServiceLocatorProviderRepository(r.ResolveAll<IServiceLocatorProvider>())).SingleInstance();
                  builder.Register(static r=>new ServiceLocator(
                      r.Resolve<ServiceLocatorProviderRepository>(),
                      r.Resolve<IScenesManager>(),
                      r.Resolve<IEnvironment>(),
                      r.Resolve<ServiceLocatorConfiguration>(),
                      r.Resolve<IClusterSerializer>(),
                      r.Resolve<RecyclableMemoryStreamProvider>(),
                      r.Resolve<ISceneHost>(),
                      r.Resolve<IHost>(),
                      r.Resolve<ServiceLocatorHostDatabase>(),
                      r.Resolve<ILogger>())
                  ).As<IServiceLocator>().InstancePerRequest();
                  
                  builder.Register(static r=> new ServiceLocatorDbApiHandler(
                      r.Resolve<ServiceLocatorHostDatabase>())
                  ).As<IApiHandler>();

                  builder.Register(static r=>new ServiceLocatorHostDatabase()).SingleInstance();
              };
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                {
                    scene.AddController<LocatorController>();
                }
            };
            ctx.SceneShuttingDown += (ISceneHost scene) =>
             {
                 var db = scene.DependencyResolver.Resolve<ServiceLocatorHostDatabase>();
                 db.RemoveScene(scene);
             };
        }

    }
}
