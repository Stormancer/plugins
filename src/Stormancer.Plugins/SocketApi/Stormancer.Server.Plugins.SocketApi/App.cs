using Stormancer.Core;
using Stormancer.Plugins;
using System;

namespace Stormancer.Server.Plugins.SocketApi
{
    /// <summary>
    /// Plugin entrypoint
    /// </summary>
    public class App
    {
        /// <summary>
        /// Method called by the framework to initialize the plugin.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new SocketPlugin());
        }
    }


    internal class SocketPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.socketApi";
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<SocketController>();
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<SocketController>();
                }
            };
        }
    }
}
