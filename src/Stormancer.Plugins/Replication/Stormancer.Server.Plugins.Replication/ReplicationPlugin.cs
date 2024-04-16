using Microsoft.AspNetCore.Builder;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Replication
{
    /// <summary>
    /// Constants associated with the plugin.
    /// </summary>
    public static class ReplicationConstants
    {
        /// <summary>
        /// Key used for the scene metadata of the replication feature.
        /// </summary>
        public const string REP_METADATA_KEY = "stormancer.server.plugins.replication";

        /// <summary>
        /// key used for the scene metadata of the lockstep feature.
        /// </summary>
        public const string LOCKSTEP_METADATA_KEY = "stormancer.lockstep";

        /// <summary>
        /// Protocol version
        /// </summary>
        public const string PROTOCOL_VERSION = "2020_09_18_1";
    }

    /// <summary>
    /// Entry point
    /// </summary>
    public class App
    {
        /// <summary>
        /// Entry point for the Replication plugin. For internal use, do not call this.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ReplicationPlugin());
        }
    }
    class ReplicationPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ReplicationController>();
                builder.Register<LockstepController>();
                builder.Register<LockstepState>().InstancePerScene();
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.TryGetEntityReplicationVersion(out _))
                {
                    scene.AddController<ReplicationController>();
                }
                if (scene.TryGetLockstepVersion(out _))
                {
                    scene.AddController<LockstepController>();
                    
                }
            };
        }
    }
}


