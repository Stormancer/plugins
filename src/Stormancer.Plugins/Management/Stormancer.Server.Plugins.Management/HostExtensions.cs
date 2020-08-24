using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server;
using Stormancer.Server.Plugins.Management;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Extensions to the <see cref="IHost"/> class.
    /// </summary>
    public static class HostExtensions
    {

        /// <summary>
        /// Ensures a scene is created on the server.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="sceneUri"></param>
        /// <param name="templateId"></param>
        /// <param name="isPublic"></param>
        /// <param name="isPersistent"></param>
        /// <param name="metadata"></param>
        public static void EnsureSceneExists(this IHost host, string sceneUri, string templateId, bool isPublic , bool isPersistent, JObject? metadata = null )
        {
            async Task createScene()
            {
                try
                {
                    await host.DependencyResolver.Resolve<ManagementClientProvider>().CreateScene(sceneUri, templateId, isPersistent, isPublic, metadata);
                }
                catch(Exception ex)
                {
                    host.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "management", $"Failed to create scene {sceneUri} of template {templateId}.", ex);
                }
            }

            _ = createScene();
        }
    }
}
