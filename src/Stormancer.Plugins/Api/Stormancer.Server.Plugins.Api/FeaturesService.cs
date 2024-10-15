
using Microsoft.Extensions.Primitives;
using Stormancer.Server.Plugins.Configuration;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.API
{
    /// <summary>
    /// Feature flags configuration.
    /// </summary>
    public class FeaturesConfigurationSection : IConfigurationSection<FeaturesConfigurationSection>
    {
        ///<inheritdoc/>
        public static string SectionPath { get; } = "features";

        ///<inheritdoc/>
        public static FeaturesConfigurationSection Default { get; } = new FeaturesConfigurationSection();

        /// <summary>
        /// Feature flags.
        /// </summary>
        public Dictionary<string, bool> flags { get; set; } = new Dictionary<string, bool>();
    }

    /// <summary>
    /// Provides feature flag functionalities.
    /// </summary>
    /// <remarks>
    /// Feature flags are set in the "features.flags" configuration section:
    ///     "features":{
    ///         "flags":{
    ///             "myNewFeature":false,
    ///             "myOtherFeature":true
    ///         }
    ///     }
    /// 
    /// By default, feature flags are enabled.
    /// </remarks>
    public class FeaturesService
    {
        private readonly ConfigurationMonitor<FeaturesConfigurationSection> config;

      
        internal FeaturesService(ConfigurationMonitor<FeaturesConfigurationSection> config)
        {
            this.config = config;
        }

        /// <summary>
        /// Gets the value of a feature flag.
        /// </summary>
        /// <remarks>
        /// If the flag is not set in the configuration, it's considered enabled.
        /// </remarks>
        /// <param name="flag"></param>
        /// <returns></returns>
        public bool GetFeatureFlag(string flag)
        {
            return !config.Value.flags.TryGetValue(flag, out var flagValue) || flagValue;
        }

    }
}