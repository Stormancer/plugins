using Stormancer.Core;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods for <see cref="ISceneHost"/>.
    /// </summary>
    public static class AnalyticsExtensions
    {
        /// <summary>
        /// Adds Analytics functionalities to the scene.
        /// </summary>
        /// <param name="scene"></param>
        public static void AddAnalytics(this ISceneHost scene)
        {
            scene.Metadata[AnalyticsPlugin.METADATA_KEY] = "enabled";
        }
    }
}
