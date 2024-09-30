using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Epic;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    /// <summary>
    /// Epic Plugin
    /// </summary>
    public class EpicPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.plugins.epic";

        /// <summary>
        /// IoC Registrations
        /// </summary>
        /// <param name="ctx"></param>
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<EpicController>().InstancePerRequest();
                builder.Register<EpicProfilePartBuilder>().As<IProfilePartBuilder>();
                builder.Register<EpicService>().As<IEpicService>();
                builder.Register(static r=>EpicServiceLocator.Instance).As<IServiceLocatorProvider>();
                builder.Register<EpicFriendsEventHandler>().As<IFriendsEventHandler>().InstancePerRequest(); 
                builder.Register<EpicAuthenticationProvider>().As<IAuthenticationProvider>();
            };

          

            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    scene.AddEpic();
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<EpicController>();
                }
            };
        }
    }

    internal class EpicServiceLocator : IServiceLocatorProvider
    {
        public static EpicServiceLocator Instance { get; } = new EpicServiceLocator();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.epic")
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
    /// Epic plugin extension methods.
    /// </summary>
    public static class EpicExtensions
    {
        /// <summary>
        /// Adds the Epic plugin on the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static ISceneHost AddEpic(this ISceneHost scene)
        {
            scene.TemplateMetadata[EpicPlugin.METADATA_KEY] = "enabled";
            return scene;
        }
    }
}
