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
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.GameSession;

namespace Stormancer.Server.Plugins.GameHistory
{
    class GameHistoryPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(static r=>new GameHistoryService(r.Resolve<GameHistoryStorage>()));
                builder.Register(static r=> new GameHistoryStorage(r.Resolve<DbContextAccessor>()));
                builder.Register(static r=> new GameHistoryGameSessionEventHandler(r.Resolve<GameHistoryService>(),r.Resolve<DbContextAccessor>(),r.Resolve<ISceneHost>(),r.Resolve<ILogger>())).As<IGameSessionEventHandler>().InstancePerRequest();
                builder.Register<GameHistoryDbModelBuilder>(static r=> new GameHistoryDbModelBuilder()).As<IDbModelBuilder>();
                builder.Register(static r => new AdminWebApiConfig()).As<IAdminWebApiConfig>().SingleInstance();
                builder.Register(static r => new GameHistoryAdminController(r.Resolve<DbContextAccessor>())).InstancePerRequest();
            };
        }
    }
}
