using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.Replication
{
    public static class ReplicationConstants
    {
        public const string METADATA_KEY = "stormancer.server.plugins.replication";
        public const string PROTOCOL_VERSION = "2020_09_18_1";
    }

    class ReplicationPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ReplicationController>();
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                scene.AddController<ReplicationController>();
            };
        }
    }
}


