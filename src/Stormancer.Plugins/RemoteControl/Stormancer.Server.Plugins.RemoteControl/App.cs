using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    /// <summary>
    /// Entry point of the plugin
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point method.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new RemoteControlPlugin());
        }
    }

    internal class RemoteControlPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
              {
                  builder.Register<AgentAuthenticationProvider>().As<IAuthenticationProvider>();
                  builder.Register<RemoteControlController>();
                  builder.Register<RemoteControlService>().As<IRemoteControlService>().AsSelf();
                  builder.Register<AgentRepository>().InstancePerScene();
                  builder.Register<RemoteControlApiServiceLocator>().As<IServiceLocatorProvider>();

              };
            ctx.HostStarting += (IHost host) =>
              {
                  host.ConfigureUsers(b =>
                  {
                      b.Settings[RemoteControlConstants.AUTHPROVIDER_TYPE] = JObject.FromObject(new AgentAuthenticationConfigurationBuilder { enabled = true });
                      return b;
                  });
                  host.AddSceneTemplate("agents", scene =>
                  {
                      scene.AddController<RemoteControlController>();
                  });
              };
            ctx.HostStarted += (IHost host) =>
              {
                  host.EnsureSceneExists("agents", "agents", false, true);
              };

        }
    }

    internal class RemoteControlApiServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if(ctx.ServiceType == "stormancer.remoteControl")
            {
                ctx.SceneId = "agents";
            }

            return Task.CompletedTask;
        }
    }
}