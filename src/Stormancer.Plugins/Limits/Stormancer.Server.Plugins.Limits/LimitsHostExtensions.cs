using Stormancer.Server;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Limits;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Contains extension methods for <see cref="IHost"/>
    /// </summary>
    public static class LimitsHostExtensions
    {
        /// <summary>
        /// Configures limits.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="configurator"></param>
        /// <returns></returns>
        public static IHost ConfigureLimits(this IHost host,Func<LimitsConfigurationBuilder,LimitsConfigurationBuilder> configurator)
        {
            var config = host.DependencyResolver.Resolve<IConfiguration>();
            var limitsConfig = new LimitsConfiguration();

            var builder = new LimitsConfigurationBuilder(limitsConfig);
            configurator(builder);
            config.SetDefaultValue("limits", limitsConfig);
            return host;
        }
    }
}
