using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.PartyMerging.admin;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    /// <summary>
    /// Entry point class
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point method
        /// </summary>
        /// <param name="builder"></param>
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
                builder.Register<PartyMergingService>().InstancePerRequest();
                builder.Register<PartyMergingState>().InstancePerScene();
                builder.Register<PartyMergerController>().InstancePerRequest();
                builder.Register<PartyMergingController>().InstancePerRequest();
                builder.Register<PartyMergingConfigurationRepository>().SingleInstance();
                builder.Register<PartyMergerLocator>().As<IServiceLocatorProvider>();
                builder.Register<PartyMergingAdminController>().InstancePerRequest();
                builder.Register<AdminWebApiConfig>().As<IAdminWebApiConfig>();

                builder.Register<MergingPartyService>().As<IMergingPartyService>().InstancePerRequest();
                builder.Register<MergingRequestPartyState>().InstancePerScene();
                builder.Register<PartyEventHandler>().As<IPartyEventHandler>();

            };
            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(PartyMergingConstants.PARTYMERGER_TEMPLATE, scene =>
                {
                    scene.Metadata.Add(PartyMergingConstants.MERGER_METADATA_KEY, PartyMergingConstants.GetVersion());
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
                    scene.Metadata.Add(PartyMergingConstants.PARTY_METADATA_KEY, PartyMergingConstants.GetVersion());
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
                    scene.Metadata.Add(PartyMergingConstants.PARTY_METADATA_KEY,PartyMergingConstants.GetVersion());
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
        /// <summary>
        /// Gets the key used to store metadata for the plugin in the party scene.
        /// </summary>
        public const string PARTY_METADATA_KEY = "stormancer.partyMerging";

        /// <summary>
        /// Gets the key used to store metadata for the plugin in the merger scene.
        /// </summary>
        public const string MERGER_METADATA_KEY = "stormancer.partyMerger";

        /// <summary>
        /// Gets the id of the merger service.
        /// </summary>
        public const string PARTYMERGER_SERVICE_TYPE = "stormancer.partyMerger";

        /// <summary>
        /// Gets the prefix for merger scenes.
        /// </summary>
        public const string PARTYMERGER_PREFIX = "partyMerger-";

        /// <summary>
        /// Gets the template for merger scenes.
        /// </summary>
        public const string PARTYMERGER_TEMPLATE = "partyMerger";

        /// <summary>
        /// Tries getting the merger id from a merger scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="mergerId"></param>
        /// <returns></returns>
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
        public static string GetVersion()
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
