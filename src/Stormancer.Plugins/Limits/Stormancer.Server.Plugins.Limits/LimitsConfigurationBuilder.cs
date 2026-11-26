namespace Stormancer.Server.Plugins.Limits
{
    /// <summary>
    /// Limits configuration builder.
    /// </summary>
    public class LimitsConfigurationBuilder
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        public LimitsConfigurationSection Config { get;  }

        internal LimitsConfigurationBuilder(LimitsConfigurationSection config)
        {
            this.Config = config;
        }
    }
}