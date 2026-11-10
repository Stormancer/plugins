using MessagePack;
using Nest;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Epic
{
    internal class EACGameSessionEventHandler : IGameSessionEventHandler
    {
        private readonly ISceneHost _scene;

        public EACGameSessionEventHandler(ISceneHost scene)
        {
            _scene = scene;
        }

        public EasyAntiCheatMode Mode => _scene.TemplateMetadata.TryGetValue("eac", out var modeStr) ? Enum.Parse<EasyAntiCheatMode>(modeStr) : EasyAntiCheatMode.Disabled;


        Task IGameSessionEventHandler.OnTopologyUpdated(TopologyUpdatedContext ctx)
        {
            return Task.CompletedTask;
        }



    }

    /// <summary>
    /// Provides client Api related to EasyAntiCheat.
    /// </summary>
    public class EacController : ControllerBase
    {
        private readonly IUserSessions _sessions;

        /// <summary>
        /// Creates a new <see cref="EacController"/> object.
        /// </summary>
        /// <param name="sessions"></param>
        public EacController(IUserSessions sessions)
        {
            _sessions = sessions;
        }


        /// <summary>
        /// Gets the Eac client options to be used for a peer.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<EacPeerInfos> GetEacClientOptions(SessionId sessionId, CancellationToken cancellationToken)
        {
            var session = await _sessions.GetSessionById(sessionId, cancellationToken);
            if(session == null)
            {
                return new EacPeerInfos { IsProtected = true, ProductUserId = "", OsPlatform = "" };
            }
            
            if (!session.Identities.TryGetValue("eos", out var productUserId))
            {
                return new EacPeerInfos { IsProtected = true, ProductUserId = "", OsPlatform = "" };
            }

            if (session.platformId.Platform.StartsWith("gameServer"))
            {
                return new EacPeerInfos { IsProtected = false, OsPlatform = "", ProductUserId = productUserId };
            }

            return new EacPeerInfos { IsProtected = true, OsPlatform = "", ProductUserId = productUserId };
        }
    }

    /// <summary>
    /// Trusted informations about a peer used by EasyAntiCheat to register a peer.
    /// </summary>
    [MessagePackObject]
    public class EacPeerInfos
    {
        /// <summary>
        /// The EOS product user id of the peer.
        /// </summary>
        [Key(0)]
        public required string ProductUserId { get; init; }

        /// <summary>
        /// Value indicating if the peer should be protected by EAC.
        /// </summary>
        [Key(1)]
        public required bool IsProtected { get; init; }

        /// <summary>
        /// The Operating system the peer is running on.
        /// </summary>
        [Key(2)]
        public required string OsPlatform { get; init; }
    }
}
