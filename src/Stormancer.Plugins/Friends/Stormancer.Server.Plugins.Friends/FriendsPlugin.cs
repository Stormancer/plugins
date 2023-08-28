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
using Stormancer.Server.Plugins.ServiceLocator;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Friends
{
    class FriendsPlugin : IHostPlugin
    {
        public const string SERVICE_ID = "stormancer.friends";
        public const string SCENE_ID = "friends";
        public const string TEMPLATE_ID = "friends";
        internal const string METADATA_KEY = "stormancer.friends";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<FriendsController>().InstancePerRequest();
                builder.Register<FriendsSceneLocator>().As<IServiceLocatorProvider>().InstancePerRequest();
                builder.Register<FriendsRepository>().InstancePerScene();
            };
            
            ctx.SceneDependenciesRegistration+=(IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    builder.Register<FriendsService>().As<IFriendsService>().InstancePerRequest();
                }
                else
                {
                    builder.Register(dr=>new FriendsServiceProxy(dr.Resolve<FriendsProxy>())).As<IFriendsService>().InstancePerRequest();
                }
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(TEMPLATE_ID, (ISceneHost sceneHost) =>
                {
                    sceneHost.AddFriends();
                });
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(SCENE_ID, TEMPLATE_ID, false, true);
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<FriendsController>();
                }
            };

            ctx.SceneStarted += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    //scene.DependencyResolver.Resolve<IFriendsService>();
                }
            };
        }
    }

    class FriendsSceneLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            switch (ctx.ServiceType)
            {
                case FriendsPlugin.SERVICE_ID:
                    ctx.SceneId = FriendsPlugin.SCENE_ID;
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
    }
}
