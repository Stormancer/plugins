using Stormancer.Plugins;
using Stormancer;
using System.Reflection;
using Stormancer.Core;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// Plugin entry point class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Plugin entry point.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new PlayerReportsPlugin());
        }
    }

    internal class PlayerReportsPlugin : IHostPlugin
    {

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ReportsController>().InstancePerRequest();
                builder.Register<ReportsService>().InstancePerRequest();
                builder.Register<ModelConfigurator>().As<IDbModelBuilder>();
                builder.Register<PlayerReportsServiceLocator>().As<IServiceLocatorProvider>();

            };
            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(PlayerReportsConstants.TEMPLATE_ID, scene =>
                {
                    scene.Metadata.Add(PlayerReportsConstants.METADATA_ID, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");
                });
            };
            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(PlayerReportsConstants.SCENE_ID, PlayerReportsConstants.TEMPLATE_ID, false, true);
            };
            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if(scene.Metadata.ContainsKey(PlayerReportsConstants.METADATA_ID))
                {
                    scene.AddController<ReportsController>();
                }
            
            };
        }
    }

    internal class PlayerReportsServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if(ctx.ServiceType == PlayerReportsConstants.SERVICE_ID)
            {
                ctx.SceneId = PlayerReportsConstants.SCENE_ID;
               
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Plugin constants.
    /// </summary>
    public static class PlayerReportsConstants
    {
        /// <summary>
        /// Id of the scene hosting the reports service.
        /// </summary>
        public const string SCENE_ID = "stormancer-reports";

        /// <summary>
        /// Id the template of the reports service scene.
        /// </summary>
        /// <remarks>
        ///  Extend this template to add new services to this scene.
        /// </remarks>
        public const string TEMPLATE_ID = "stormancer.reports";

        public const string SERVICE_ID = "stormancer.reports";
        public const string METADATA_ID = "stormancer.reports";
    }
}
