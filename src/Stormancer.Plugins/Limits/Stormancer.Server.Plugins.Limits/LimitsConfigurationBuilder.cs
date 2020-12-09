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
        public LimitsConfiguration Config { get;  }

        internal LimitsConfigurationBuilder(LimitsConfiguration config)
        {
            this.Config = config;
        }
    }
}