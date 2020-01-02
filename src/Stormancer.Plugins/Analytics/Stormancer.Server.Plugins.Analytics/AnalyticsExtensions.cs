using Stormancer.Core;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class AnalyticsExtensions
    {
        public static void AddAnalytics(this ISceneHost scene)
        {
            scene.Metadata[AnalyticsPlugin.METADATA_KEY] = "enabled";
        }
    }
}
