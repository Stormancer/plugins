﻿using Stormancer.Core;
using Stormancer.Diagnostics;
using Stormancer.Plugins;

namespace Stormancer.Plugins.RemoteControl
{
    public class RemoteControlAgentPlugin : IClientPlugin
    {
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.Register(dr => new RemoteControlAgentApi(dr.Resolve<UserApi>(),dr.Resolve<ILogger>()), true);
                client.DependencyResolver.Register(dr => new RemoteControlConfiguration(), true);
                client.DependencyResolver.Register<IAuthenticationEventHandler>(dr => new RemoteControlledAgentAuthEventHandler(dr.Resolve<RemoteControlConfiguration>()));
            };
        }
    }

    public class RemoteControlClientPlugin : IClientPlugin
    {
        public void Build(PluginBuildContext ctx)
        {
            ctx.ClientCreated += (Client client) =>
            {
                client.DependencyResolver.Register(dr => new RemoteControlClientApi(dr.Resolve<UserApi>()), true);
            };

            ctx.RegisterSceneDependencies += (Scene scene, IDependencyResolver dr) =>
            {
                if(scene.Id == "agents")
                {
                    dr.Register(dr => new RemoteControlClientService(scene,dr.Resolve<ISerializer>()), true);
                }
            };
        }
    }
}