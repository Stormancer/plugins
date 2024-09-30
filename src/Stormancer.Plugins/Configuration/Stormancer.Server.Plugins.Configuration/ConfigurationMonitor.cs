using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Configuration
{
    /// <summary>
    /// Base class for configuration sections.
    /// </summary>
    public interface IConfigurationSection<T>
    {
        /// <summary>
        /// Gets the path to this section in the configuration.
        /// </summary>
        static abstract string SectionPath { get; }

        /// <summary>
        /// Gets the default value for the section.
        /// </summary>
        static abstract T Default { get; }
    }

    /// <summary>
    /// Tacks a configuration object
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConfigurationMonitor<T> : IConfigurationChangedEventHandler where T : IConfigurationSection<T>
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Gets the current value of the configuration section.
        /// </summary>
        public T Value { get; private set; }
    
        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationMonitor{T}"/>
        /// </summary>
        /// <param name="configuration"></param>
        public ConfigurationMonitor(IConfiguration configuration)
        {

            this._configuration = configuration;
            Value = _configuration.GetValue<T>(T.SectionPath) ?? T.Default;
        }

        void IConfigurationChangedEventHandler.OnConfigurationChanged()
        {
            Value = _configuration.GetValue<T>(T.SectionPath) ?? T.Default;
        }
    }
}
