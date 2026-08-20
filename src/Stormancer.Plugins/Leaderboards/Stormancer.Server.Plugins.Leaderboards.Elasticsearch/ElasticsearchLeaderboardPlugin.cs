using Stormancer.Plugins;
using Stormancer.Server.Plugins.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Stormancer.Server.Plugins.Leaderboards.Elasticsearch.ILeaderboarIndexMapping;

namespace Stormancer.Server.Plugins.Leaderboards.Elasticsearch
{
    class ElasticsearchLeaderboardPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ElasticsearchLeaderboardStorage>(dr => new ElasticsearchLeaderboardStorage(
                    dr.Resolve<IESClientFactory>(),
                    dr.Resolve<Func<IEnumerable<ILeaderboardIndexMapping>>>()
                    )).As<ILeaderboardStorage>();
            };
        }
    }
}
