using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Galaxy;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Galaxy
{
    internal class GalaxyPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.plugins.galaxy";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<GalaxyProfilePartBuilder>().As<IProfilePartBuilder>();
                builder.Register<GalaxyService>().As<IGalaxyService>();
            };

            ctx.SceneDependenciesRegistration += (builder, scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    builder.Register<GalaxyAuthenticationProvider>().As<IAuthenticationProvider>();
                }
            };

            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    scene.AddGalaxy();
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                }
            };
        }
    }

    internal class GalaxyServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.galaxy")
            {
                ctx.SceneId = Constants.GetSceneId();
            }
            return Task.CompletedTask;
        }
    }
}

namespace Stormancer
{
    /// <summary>
    /// Galaxy plugin extension methods.
    /// </summary>
    public static class GalaxyExtensions
    {
        /// <summary>
        /// Adds the Galaxy plugin on the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static ISceneHost AddGalaxy(this ISceneHost scene)
        {
            scene.Metadata[GalaxyPlugin.METADATA_KEY] = "enabled";
            return scene;
        }
    }
}
