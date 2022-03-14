
using Newtonsoft.Json.Linq;
using Stormancer.Server;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer
{
    /// <summary>
    /// User configuration extension methods.
    /// </summary>
    public static class UsersConfigurationExtensions
    {
        /// <summary>
        /// Adds default configuration for users.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        /// <remarks>Configuration settings can be overrided in the App configuration json file on the grid.</remarks>
        public static IConfiguration ConfigureUsers(this IConfiguration config, Func<UsersConfigurationBuilder, UsersConfigurationBuilder> builder)
        {
            var settings = config.GetValue("auth",new Dictionary<string, JObject>());
            var configBuilder = new UsersConfigurationBuilder(settings);

            configBuilder = builder(configBuilder);
            config.SetDefaultValue("auth", configBuilder.Settings);

            return config;
        }

        /// <summary>
        /// Adds default configuration for users.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        /// <remarks>Configuration settings can be overrided in the App configuration json file on the grid.</remarks>
        public static IHost ConfigureUsers(this IHost host, Func<UsersConfigurationBuilder, UsersConfigurationBuilder> builder)
        {
            host.DependencyResolver.Resolve<IConfiguration>().ConfigureUsers(builder);
            return host;
        }
    }
    
}
namespace Stormancer.Server.Plugins.Users
{
    /// <summary>
    /// Provides methods to configure the users plugin.
    /// </summary>
    public class UsersConfigurationBuilder
    {
        internal UsersConfigurationBuilder(Dictionary<string, JObject> settings)
        {
            Settings = settings;
        }
        /// <summary>
        /// Settings object
        /// </summary>
        public Dictionary<string, JObject> Settings { get; }
    }
}