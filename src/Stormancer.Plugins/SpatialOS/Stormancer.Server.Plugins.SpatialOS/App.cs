using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.SpatialOS
{
    /// <summary>
    /// Entrypoint for the SpatialOS plugin. Internal use, do not call it.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entrypoint for the SpatialOS plugin. Internal use, do not call it.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new SpatialOSPlugin());
        }
    }
}
