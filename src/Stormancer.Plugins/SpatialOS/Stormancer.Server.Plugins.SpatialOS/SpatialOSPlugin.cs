using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.SpatialOS
{
    class SpatialOSPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.spatialoscredentials";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<SpatialOSCredentialsController>().InstancePerRequest();
                builder.Register<SpatialOSCredentialsService>().As<ISpatialOSCredentialsService>().InstancePerRequest();
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<SpatialOSCredentialsController>();
                }
            };
        }
    }
}

namespace Stormancer
{
    /// <summary>
    /// Extensions methods to add SpatialOS functionnalities to scenes
    /// </summary>
    public static class SpatialOSExtensions
    {
        /// <summary>
        /// Adds a SpatialOSOSCredentialsController to the scene.
        /// </summary>
        /// <param name="scene">The affected scene</param>
        /// <returns>The affected scene</returns>
        public static ISceneHost AddSpatialOsCredentials(this ISceneHost scene)
        {
            scene.TemplateMetadata[Stormancer.Server.Plugins.SpatialOS.SpatialOSPlugin.METADATA_KEY] = "enabled";
            return scene;
        }
    }
}
