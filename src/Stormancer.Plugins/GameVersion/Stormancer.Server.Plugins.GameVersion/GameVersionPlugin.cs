﻿// MIT License
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
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;

namespace Stormancer.Server.Plugins.GameVersion
{
    internal class GameVersionPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.gameVersion";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    builder.Register(static r=> new GameVersionService(r.Resolve<IConfiguration>(),r.Resolve<Components.IEnvironment>(),r.Resolve<ISceneHost>())).AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();
                    builder.Register(static r=> new AuthenticationEventHandler(r.Resolve<GameVersionService>(),r.Resolve<IUserSessions>(),r.Resolve<Diagnostics.ILogger>())).As<IAuthenticationEventHandler>().InstancePerRequest();
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.DependencyResolver.Resolve<GameVersionService>();
                }
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(Users.Constants.SCENE_TEMPLATE, (ISceneHost scene) =>
                {
                    scene.AddGameVersion();
                });
            };
        }
    }
}
