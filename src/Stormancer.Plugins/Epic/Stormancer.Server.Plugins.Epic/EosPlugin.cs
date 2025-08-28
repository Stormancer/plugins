using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Eos;
using Stormancer.Server.Plugins.Friends;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Eos
{
    /// <summary>
    /// Epic Plugin
    /// </summary>
    public class EosPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.plugins.eos";

        /// <summary>
        /// IoC Registrations
        /// </summary>
        /// <param name="ctx"></param>
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<EosController>().InstancePerRequest();
                builder.Register<EosProfilePartBuilder>().As<IProfilePartBuilder>();
                builder.Register<EosService>().As<IEosService>();
                builder.Register(static r=>EpicServiceLocator.Instance).As<IServiceLocatorProvider>();
                builder.Register<EosFriendsEventHandler>().As<IFriendsEventHandler>().InstancePerRequest(); 
                builder.Register<EosAuthenticationProvider>().As<IAuthenticationProvider>();
            };

          

            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Template == Constants.SCENE_TEMPLATE)
                {
                    scene.AddEos();
                }
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<EosController>();
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
        public static ISceneHost AddEos(this ISceneHost scene)
        {
            scene.TemplateMetadata[EosPlugin.METADATA_KEY] = "enabled";
            return scene;
        }
    }
}
