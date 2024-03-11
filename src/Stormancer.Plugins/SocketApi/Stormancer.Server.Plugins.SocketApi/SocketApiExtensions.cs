using Stormancer.Core;
using Stormancer.Server.Plugins.SocketApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extension methods
    /// </summary>
    public static class SocketApiExtensions
    {
        /// <summary>
        /// Adds the socket relay API to a scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static ISceneHost AddSocket(this ISceneHost scene)
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            scene.TemplateMetadata[SocketPlugin.METADATA_KEY] = currentAssembly.GetName()?.Version?.ToString() ?? "0.0.0";
            return scene;
        }
    }
}
