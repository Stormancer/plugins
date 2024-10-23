using Stormancer.Plugins;
using Stormancer;
using System.Reflection;
using Stormancer.Core;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Threading.Tasks;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.BlobStorage;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.PlayerReports.Admin;

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
                builder.Register(r => new ReportsController(r.Resolve<ReportsService>(), r.Resolve<IUserSessions>())).InstancePerRequest();
                builder.Register(r => new ReportsService(r.Resolve<DbContextAccessor>(), r.ResolveAll<IBugReportingBackend>(), r.Resolve<ILogger>(), r.Resolve<ConfigurationMonitor<BugReportsConfigurationSection>>())).InstancePerRequest();
                builder.Register(r => ModelConfigurator.Instance).As<IDbModelBuilder>();
                builder.Register(r => PlayerReportsServiceLocator.Instance).As<IServiceLocatorProvider>();
                builder.Register(r => new InternalBugReportBackend(r.Resolve<IBlobStorage>(), r.Resolve<DbContextAccessor>(), r.Resolve<ConfigurationMonitor<BugReportsInternalBackendConfigurationSection>>())).As<IBugReportingBackend>().InstancePerRequest();

                builder.Register(r => new ConfigurationMonitor<BugReportsConfigurationSection>(r.Resolve<IConfiguration>())).AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();
                builder.Register(r => new ConfigurationMonitor<BugReportsInternalBackendConfigurationSection>(r.Resolve<IConfiguration>())).AsSelf().As<IConfigurationChangedEventHandler>().SingleInstance();

                builder.Register(r => new AdminWebApiConfig()).As<IAdminWebApiConfig>();
                builder.Register(r => new PlayerReportsAdminController(r.Resolve<DbContextAccessor>())).InstancePerRequest();

            };
            IHost? _host = null;
            ctx.HostStarting += (IHost host) =>
            {
                _host = host;
                host.AddSceneTemplate(PlayerReportsConstants.TEMPLATE_ID, scene =>
                {
                    scene.TemplateMetadata.Add(PlayerReportsConstants.METADATA_ID, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");
                });
            };
            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(PlayerReportsConstants.SCENE_ID, PlayerReportsConstants.TEMPLATE_ID, false, true);
            };
            ctx.SceneCreated += (ISceneHost scene) =>
            {

                //_host?.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "test", "Adding player reports controller.", new { sceneId = scene.Id, metadata = scene.Metadata });
                if (scene.TemplateMetadata.ContainsKey(PlayerReportsConstants.METADATA_ID))
                {
                    //_host?.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error,"test", "Added player reports controller.", new { });
                    scene.AddController<ReportsController>();
                }

            };
        }
    }

    internal class PlayerReportsServiceLocator : IServiceLocatorProvider
    {
        public static PlayerReportsServiceLocator Instance { get; } = new PlayerReportsServiceLocator();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == PlayerReportsConstants.SERVICE_ID)
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

        /// <summary>
        /// Service id used by the client.
        /// </summary>
        public const string SERVICE_ID = "stormancer.reports";

        /// <summary>
        /// Metadata id detected by the client.
        /// </summary>
        public const string METADATA_ID = "stormancer.reports";
    }
}
