using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Configuration
{
    internal class ConfigurationNotifier
    {
        private readonly IHost host;
        private readonly ILogger logger;

        public ConfigurationNotifier(IEnvironment env, IHost host, ILogger logger)
        {
            env.ConfigurationChanged += Env_ConfigurationChanged;
            env.ActiveDeploymentChanged += Env_ActiveDeploymentChanged;
            this.host = host;
            this.logger = logger;
        }

        private void Env_ActiveDeploymentChanged(object? sender, ActiveDeploymentChangedEventArgs e)
        {
            var deps = host.DependencyResolver.ResolveAll<IConfigurationChangedEventHandler>();
            deps.RunEventHandler(h => h.OnDeploymentChanged(), ex => logger.Log(LogLevel.Error, "configuration", $"An error occured while running {nameof(IConfigurationChangedEventHandler.OnDeploymentChanged)}", ex));
            foreach (var scene in host.EnumerateScenes())
            {
                scene.DependencyResolver.ResolveAll<IConfigurationChangedEventHandler>().Where(dep => !deps.Contains(dep)).RunEventHandler(h => h.OnDeploymentChanged(), ex => logger.Log(LogLevel.Error, "configuration", $"An error occured while running {nameof(IConfigurationChangedEventHandler.OnDeploymentChanged)}", ex));

            }

        }

        private void Env_ConfigurationChanged(object? sender, EventArgs e)
        {

            NotifyConfigChanged();
        }

        internal void NotifyConfigChanged()
        {
            var deps = host.DependencyResolver.ResolveAll<IConfigurationChangedEventHandler>();
            deps.RunEventHandler(h => h.OnConfigurationChanged(), ex => logger.Log(LogLevel.Error, "configuration", $"An error occured while running {nameof(IConfigurationChangedEventHandler.OnConfigurationChanged)}", ex));

            foreach(var scene in host.EnumerateScenes())
            {
                scene.DependencyResolver.ResolveAll<IConfigurationChangedEventHandler>().Where(dep=>!deps.Contains(dep)).RunEventHandler(h => h.OnConfigurationChanged(), ex => logger.Log(LogLevel.Error, "configuration", $"An error occured while running {nameof(IConfigurationChangedEventHandler.OnConfigurationChanged)}", ex));

            }
        }
    }
}
