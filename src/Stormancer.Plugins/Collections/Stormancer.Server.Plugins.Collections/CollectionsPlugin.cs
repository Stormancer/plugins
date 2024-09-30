using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Profile;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Collections
{
    internal class CollectionsPlugin : IHostPlugin
    {
        public const string SERVICE_ID = "stormancer.collections";
        public const string METADATA_KEY = "stormancer.collections";
        public const string SCENE_ID = "collectionService";
        public const string SCENE_TEMPLATE = SCENE_ID;
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<CollectionsController>().InstancePerRequest();
                builder.Register<CollectionsService>().As<ICollectionService>().InstancePerRequest();
                builder.Register<CollectionsRepository>().As<IConfigurationChangedEventHandler>().AsSelf().SingleInstance();
                builder.Register<CollectionProfilePartBuilder>().As<IProfilePartBuilder>().InstancePerRequest();
                builder.Register<CollectionServiceLocator>(r=> CollectionServiceLocator.Instance).As<IServiceLocatorProvider>();
                builder.Register<CollectionsModelConfiguration>().As<IDbModelBuilder>();
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(SCENE_TEMPLATE, scene =>
                {
                    scene.TemplateMetadata[METADATA_KEY] = "enabled";
                });
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<CollectionsController>();
                }
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(SCENE_ID, SCENE_TEMPLATE, false, true);
            };

        }
    }

    internal class CollectionServiceLocator : IServiceLocatorProvider
    {
        public static CollectionServiceLocator Instance { get; } = new CollectionServiceLocator();
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == CollectionsPlugin.SERVICE_ID)
            {
                ctx.SceneId = CollectionsPlugin.SCENE_ID;

            }
            return Task.CompletedTask;
        }
    }

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
            builder.AddPlugin(new CollectionsPlugin());
        }
    }
}
