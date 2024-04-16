using Stormancer.Core;
using Stormancer.Server.Plugins.Replication;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;


namespace Stormancer
{
    /// <summary>
    /// Provides extension methods to add replication features to a scene.
    /// </summary>
    public static class ReplicationExtensions
    {
        /// <summary>
        /// Enables entity replication features in a scene.
        /// </summary>
        /// <param name="scene"></param>
        public static ISceneHost AddEntityReplication(this ISceneHost scene)
        {
            scene.TemplateMetadata[ReplicationConstants.REP_METADATA_KEY] = ReplicationConstants.PROTOCOL_VERSION;
            return scene;
        }

        /// <summary>
        /// Tries to get the version of the replication protocol enabled in the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static bool TryGetEntityReplicationVersion(this ISceneHost scene, [NotNullWhen(true)] out string? version)
        {
            return scene.TemplateMetadata.TryGetValue(ReplicationConstants.REP_METADATA_KEY,out version);
        }

        /// <summary>
        /// Enables lockstep replication features in a scene.
        /// </summary>
        /// <param name="scene"></param>
        public static ISceneHost AddLockstep(this ISceneHost scene)
        {
            scene.AddP2PMesh();
            scene.TemplateMetadata[ReplicationConstants.LOCKSTEP_METADATA_KEY] = ReplicationConstants.PROTOCOL_VERSION;
            return scene;
        }

        /// <summary>
        /// Tries to get the version of the lockstep protocol enabled in the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        public static bool TryGetLockstepVersion(this ISceneHost scene, [NotNullWhen(true)] out string? version)
        {
            return scene.TemplateMetadata.TryGetValue(ReplicationConstants.LOCKSTEP_METADATA_KEY, out version);
        }
    }
}

