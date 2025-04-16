using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Analytics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.BlobStorage
{
    /// <summary>
    /// Entry point class.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point method of the plugin.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new BlobStoragePlugin());
        }
    }

    internal class BlobStoragePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register(dr => new BlobController(dr.Resolve<IBlobStorage>())).InstancePerRequest();
                builder.Register(dr => new BlobStorageKeyStore()).SingleInstance();
                builder.Register(dr => new BlobStorage(dr.ResolveAll<IBlobStorageBackend>(), dr.Resolve<Lazy<IEnumerable<IBlobStorageEventHandler>>>(), dr.Resolve<IConfiguration>(), dr.Resolve<BlobStorageKeyStore>())).As<IBlobStorage>().InstancePerRequest();
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(BlobStorageConstants.SCENE_ID, scene => scene.AddBlobStorage());
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(BlobStorageConstants.SCENE_ID, BlobStorageConstants.SCENE_ID, false, true);
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TemplateMetadata.ContainsKey(BlobStorageConstants.METADATA_KEY))
                {
                    scene.AddController<BlobController>();
                }
            };
        }
    }

    internal class BlobStorageServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if(ctx.ServiceType == BlobStorageConstants.METADATA_KEY)
            {
                ctx.SceneId = BlobStorageConstants.SCENE_ID;
            }
            return Task.CompletedTask;
        }
    }
}
