using Nest;
using Org.BouncyCastle.Math.EC;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.PartyMerging;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyFinder
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new PartyMergingPlugin());
        }
    }

    internal class PartyMergingPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<PartyMergingService>().InstancePerScene();
                builder.Register<PartyMergerController>().InstancePerRequest();
                builder.Register<PartyMergingController>().InstancePerRequest();
                builder.Register<PartyMergingConfigurationRepository>().SingleInstance();
                builder.Register<PartyMergerLocator>().As<IServiceLocatorProvider>();

            };
            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(PartyMergingConstants.PARTYMERGER_TEMPLATE, scene =>
                {
                    scene.Metadata.Add(PartyMergingConstants.MERGER_METADATA_KEY,);
                });
            };
            PartyMergingConfigurationRepository? configs = null;
            ctx.HostStarted += (IHost host) =>
            {
                configs = host.DependencyResolver.Resolve<PartyMergingConfigurationRepository>();
                foreach (var id in configs.Scenes)
                {
                    host.EnsureSceneExists(id, PartyMergingConstants.PARTYMERGER_TEMPLATE, false, true);
                }
            };

            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Metadata.TryGetValue(PartyMergingConstants.MERGER_METADATA_KEY, out var _) && configs != null)
                {

                    configs.ConfigureDependencyResolver(scene, builder);
                }
            };
            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Metadata.TryGetValue(PartyConstants.METADATA_KEY, out _))
                {
                    scene.Metadata.Add(PartyMergingConstants.PARTY_METADATA_KEY,)
                    }
            };
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.TryGetValue(PartyMergingConstants.MERGER_METADATA_KEY, out var _))
                {
                    scene.AddController<PartyMergerController>();


                }
                if (scene.Metadata.TryGetValue(PartyConstants.METADATA_KEY, out _))
                {
                    scene.Metadata.Add(PartyMergingConstants.)
                    scene.AddController<PartyMergingController>();
                }
            };
        }
    }

    /// <summary>
    /// Constants of the party merging plugin.
    /// </summary>
    public class PartyMergingConstants
    {
        public const string PARTY_METADATA_KEY = "stormancer.partyMerging";
        public const string MERGER_METADATA_KEY = "stormancer.partyMerger";
        public const string PARTYMERGER_SERVICE_TYPE = "stormancer.partyMerger";
        public const string PARTYMERGER_PREFIX = "partyMerger-";
        public const string PARTYMERGER_TEMPLATE = "partyMerger";
        public static bool TryGetMergerId(ISceneHost scene, [NotNullWhen(true)] out string? mergerId)
        {
            if (scene.Id.StartsWith(PARTYMERGER_PREFIX))
            {
                mergerId = scene.Id.Substring(PARTYMERGER_PREFIX.Length);
                return true;
            }
            else
            {
                mergerId = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the version of the party merging plugin.
        /// </summary>
        /// <returns></returns>
        public string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    internal class PartyMergerLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if(ctx.ServiceType == PartyMergingConstants.PARTYMERGER_SERVICE_TYPE)
            {
                ctx.SceneId = PartyMergingConstants.PARTYMERGER_PREFIX + ctx.ServiceName;
            }
            return Task.CompletedTask;
        }
    }
}
