using MessagePack;
using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Voice
{
    /// <summary>
    /// A ticket allowing an user to enter a voice chatroom.
    /// </summary>
    [MessagePackObject]
    public class VoiceChatTicket
    {
        /// <summary>
        /// Type of the ticket.
        /// </summary>
        public required string Type { get; init; }


        /// <summary>
        /// Custom arguments.
        /// </summary>
        public required string Args { get; init; }
    }
    /// <summary>
    /// Controller providing voic chat related features.
    /// </summary>
    public class VoiceChatController : ControllerBase
    {
        private readonly IEnumerable<IVoiceChatProvider> _providers;
        private readonly ConfigurationMonitor<VoiceChatConfigurationSection> _configuration;
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;

        /// <summary>
        /// Creates a new <see cref="VoiceChatController"/> object.
        /// </summary>
        /// <param name="providers"></param>
        /// <param name="configuration"></param>
        /// <param name="scene"></param>
        /// <param name="serializer"></param>
        public VoiceChatController(
            IEnumerable<IVoiceChatProvider> providers,
            ConfigurationMonitor<VoiceChatConfigurationSection> configuration,
            ISceneHost scene,
            ISerializer serializer)
        {
            _providers = providers;
            _configuration = configuration;
            _scene = scene;
            _serializer = serializer;
        }

        private bool TryGetVoiceChatProvider([NotNullWhen(true)] out IVoiceChatProvider? provider)
        {
            var config = _configuration.Value;
            var type = config.ProviderType;
            if (type == null)
            {
                provider = null;
                return false;
            }
            foreach (var p in _providers)
            {
                if (p.Type == type)
                {
                    provider = p;
                    return true;
                }
            }

            provider = null;
            return false;
        }

        /// <inheritdoc/>
        protected override async Task OnConnected(IScenePeerClient peer)
        {
            if (TryGetVoiceChatProvider(out var voiceChat))
            {
                var ticket = await voiceChat.AddUserToRoom(_scene.Id, peer.SessionId);
                _scene.Send(new MatchPeerFilter(peer), "voice.addToRoom", (writer, ticket) => { _serializer.Serialize(ticket, writer); }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE, ticket);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnDisconnected(DisconnectedArgs args)
        {
            if (TryGetVoiceChatProvider(out var voiceChat))
            {
                await voiceChat.RemoveUserFromRoom(_scene.Id, args.Peer.SessionId);
            }
        }
    }
}
