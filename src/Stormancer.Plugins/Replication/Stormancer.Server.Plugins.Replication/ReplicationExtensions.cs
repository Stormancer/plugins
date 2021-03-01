using Stormancer.Core;
using Stormancer.Server.Plugins.Replication;
using System;
using System.Collections.Generic;
using System.Text;


namespace Stormancer
{
    /// <summary>
    /// Provides extension methods to add replication features to a scene.
    /// </summary>
    public static class ReplicationExtensions
    {
        /// <summary>
        /// Adds replication features to a scene.
        /// </summary>
        /// <param name="scene"></param>
        public static void AddReplication(this ISceneHost scene)
        {
            scene.Metadata[ReplicationConstants.METADATA_KEY] = ReplicationConstants.PROTOCOL_VERSION;
        }
    }
}

