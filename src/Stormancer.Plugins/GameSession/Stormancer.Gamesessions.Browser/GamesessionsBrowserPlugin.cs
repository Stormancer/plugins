using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System.Reflection;
using System.Threading.Tasks;

namespace Stormancer.Gamesessions.Browser
{
    internal class GamesessionsBrowserPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(dr => new GamesessionBrowserController(dr.Resolve<GamesessionSearchService>())).InstancePerRequest();
                builder.Register(dr => new GamesessionBrowserDocumentController(dr.Resolve<GamesessionLuceneDocumentStore>(),dr.Resolve<IGameSessionService>(), dr.Resolve<IUserSessions>(), dr.Resolve<GamesessionSearchState>())).InstancePerRequest();
                builder.Register(dr => LocatorProvider.Instance).As<IServiceLocatorProvider>();
                builder.Register(dr => new GamesessionSearchService(dr.Resolve<SearchEngine>())).InstancePerRequest();
                builder.Register(dr => new GamesessionLuceneDocumentStore(dr.Resolve<ILucene>())).SingleInstance();
                builder.Register(dr => new GameSessionReservations(dr.Resolve<IHost>(), dr.Resolve<GameSessionsRepository>(), dr.Resolve<IClusterSerializer>())).SingleInstance();
            };

            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if(scene.TemplateMetadata.ContainsKey(GameSessionConstants.METADATA_KEY))
                {
                    builder.Register(dr => new ReservationsState()).SingleInstance();
                    builder.Register(dr => new GamesessionSearchState()).SingleInstance();
                   
                }
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(GamesessionBrowserConstants.SCENE_TYPE, (ISceneHost scene) =>
                {
                    scene.TemplateMetadata[GamesessionBrowserConstants.METADATA_KEY] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
                });
                host.DependencyResolver.Resolve<GamesessionLuceneDocumentStore>().Initialize();
                host.DependencyResolver.Resolve<GameSessionReservations>().Initialize();
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(GamesessionBrowserConstants.SCENE_ID, GamesessionBrowserConstants.SCENE_TYPE, false,true);
            };

            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(GameSessionConstants.METADATA_KEY))
                {
                    scene.AddController<GamesessionBrowserDocumentController>();
                }
                else if(scene.TemplateMetadata.ContainsKey(GamesessionBrowserConstants.METADATA_KEY))
                {
                    scene.AddController<GamesessionBrowserController>();
                }
            };

            ctx.SceneShuttingDown += (ISceneHost scene) =>
            {

                if (scene.TemplateMetadata.ContainsKey(GameSessionConstants.METADATA_KEY))
                {
                    var state = scene.DependencyResolver.Resolve<GamesessionSearchState>();
                    if (state.Document != null)
                    {
                        scene.DependencyResolver.Resolve<GamesessionLuceneDocumentStore>().DeleteDocument(scene.Id);
                    }
                }
                
            };
        }
    }

    internal class LocatorProvider : IServiceLocatorProvider
    {

        public static LocatorProvider Instance { get; } = new LocatorProvider();
        public LocatorProvider()
        {

        }
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == GamesessionBrowserConstants.METADATA_KEY)
            {

                ctx.SceneId = GamesessionBrowserConstants.SCENE_ID;
            }
            return Task.CompletedTask;

        }
    }
}