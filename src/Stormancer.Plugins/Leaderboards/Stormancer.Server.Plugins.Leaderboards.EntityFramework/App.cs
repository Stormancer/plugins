using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Leaderboards.EntityFramework
{
    public class App
    {
        /// <summary>
        /// Leaderboard Entity Framework plugin startup method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkLeaderboardPlugin());
        }
    }
}
