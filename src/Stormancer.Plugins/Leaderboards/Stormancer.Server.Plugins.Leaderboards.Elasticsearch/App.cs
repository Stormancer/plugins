using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.Elasticsearch
{
    public  class App
    {
        /// <summary>
        /// Leaderboard Elasticsearch plugin startup method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ElasticsearchLeaderboardPlugin());
        }
    }
}
