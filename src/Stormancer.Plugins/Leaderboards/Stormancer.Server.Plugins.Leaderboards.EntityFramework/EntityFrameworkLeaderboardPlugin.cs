using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Database;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Leaderboards.EntityFramework.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework
{
    internal class EntityFrameworkLeaderboardPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<EntitiyFrameworkLeaderboardStorage>(dr => new EntitiyFrameworkLeaderboardStorage(
                    dr.Resolve<DbContextAccessor>(),
                    dr.Resolve<ILogger>()
                    )).As<ILeaderboardStorage>();
                builder.Register<LeaderboardDbModelBuilder>().As<IDbModelBuilder>();
            };
        }
    }
}
