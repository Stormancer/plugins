using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Stormancer.Server.Plugins.Queries
{
    /// <summary>
    /// Plugin initialization class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Plugin startup method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ServiceQueriesPlugin());
        }
    }

}
