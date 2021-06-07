using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.AdminApi;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Limits
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
            builder.AddPlugin(new LimitsPlugin());
        }
    }
    class LimitsPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
             {
                 builder.Register<LimitsClientCache>().SingleInstance();
                 builder.Register<LimitsAdminWebApiConfig>().As<IAdminWebApiConfig>();
                 builder.Register<LimitsClient>().InstancePerRequest();
                 builder.Register<LimitsAuthenticationEventHandler>().As<IAuthenticationEventHandler>().As<IUserSessionEventHandler>().InstancePerRequest();
             };
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                {
                    scene.AddController<LimitsController>();
                }
            };
            ctx.SceneStarted += (ISceneHost scene) =>
            {
                if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                {
                    var limits = scene.DependencyResolver.Resolve<Limits>();
                    _ = scene.RunTask(limits.RunQueueAsync);
                }
            };
            ctx.SceneDependenciesRegistration += (builder, scene) =>
            {
                if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                {
                    builder.Register<Limits>().As<ILimits>().As<IConfigurationChangedEventHandler>().AsSelf().InstancePerScene();
                    builder.Register<LimitsController>();
                }
            };
        }
    }
}
