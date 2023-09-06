using Stormancer.Core;
using Stormancer.Plugins;
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
                builder.Register<CollectionsService>().InstancePerRequest();
                builder.Register<CollectionsRepository>().SingleInstance();
                builder.Register<CollectionProfilePartBuilder>().As<IProfilePartBuilder>().InstancePerRequest();
                builder.Register<CollectionServiceLocator>().As<IServiceLocatorProvider>();
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(SCENE_TEMPLATE, scene =>
                {
                    scene.Metadata[METADATA_KEY] = "enabled";
                });
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
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
