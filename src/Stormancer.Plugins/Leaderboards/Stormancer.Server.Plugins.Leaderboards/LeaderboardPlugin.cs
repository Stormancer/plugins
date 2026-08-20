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
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.Leaderboards
{
    class LeaderboardPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.leaderboard";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(dr => new LeaderboardService(
                    dr.Resolve<ILogger>(),
                    dr.Resolve<Func<IEnumerable<ILeaderboardEventHandler>>>(),
                    dr.Resolve<IFriendsService>(),
                    dr.Resolve<IConfiguration>(),
                    dr.Resolve<IUserService>(),
                    dr.Resolve<ILeaderboardStorage>()
                    )).As<ILeaderboardService>().InstancePerRequest();
                builder.Register(dr => new LeaderboardController(
                    dr.Resolve<ILeaderboardService>(),
                    dr.Resolve<IUserSessions>()
                    )).InstancePerRequest();
                builder.Register(_ => new LeaderboardsWebApiConfig()).As<IAdminWebApiConfig>();
                builder.Register(dr => new LeaderboardsAdminController(dr.Resolve<ILeaderboardService>()));
            };

            ctx.HostStarting += (IHost host) =>
            {
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<LeaderboardController>();
                }
            };


        }
    }
}
