using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Utilities
{
    /// <summary>
    /// Context passed to <see cref="IP2pEventHandler.OnGetP2PMetadata"/>.
    /// </summary>
    public class OnGetP2PMetadataContext
    {
        /// <summary>
        /// Creates a new <see cref="OnGetP2PMetadataContext"/> instance.
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="scene"></param>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        public OnGetP2PMetadataContext(Dictionary<string, string> metadata, ISceneHost scene, IScenePeerClient origin, IScenePeerClient target)
        {
            Metadata = metadata;
            Scene = scene;
            Origin = origin;
            Target = target;
        }
        /// <summary>
        /// P2P metadata
        /// </summary>
        public Dictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets the scene the event was wired in.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Gets the origin of the P2P connection request.
        /// </summary>
        public IScenePeerClient Origin { get; }

        /// <summary>
        /// Gets the target we are retrieving P2P metadata for.
        /// </summary>
        public IScenePeerClient Target { get; }
    }
    /// <summary>
    /// Provides Metadata for the modular P2P system
    /// </summary>
    public interface IP2pEventHandler
    {
        /// <summary>
        /// Called whenever a system requests P2P metadata.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        ValueTask OnGetP2PMetadata(OnGetP2PMetadataContext ctx);

    }
}
