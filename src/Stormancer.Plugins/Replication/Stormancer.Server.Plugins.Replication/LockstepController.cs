using MessagePack;
using Stormancer.Abstractions.Server;
using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Replication
{

    internal class Player
    {
        public required IScenePeerClient Peer { get; init; }
        public required int Id { get; init; }
    }
    internal class LockstepState
    {
        public int CurrentUpdateId { get; set; } = 0;
        public int NextPlayerId { get; set; } = 1;


        public ConcurrentDictionary<SessionId, Player> Players { get; } = new ConcurrentDictionary<SessionId, Player>();

        public object SyncRoot { get; set; } = new object();

        public PlayersSnapshotInstallCommand GetSnapshot(int playerId)
        {
            return new PlayersSnapshotInstallCommand { UpdateId = CurrentUpdateId, Players = Players.Values.ToDictionary(p => p.Id, p => p.Peer.SessionId), CurrentPlayerId = playerId };
        }
    }
    internal class LockstepController : ControllerBase
    {
        private readonly LockstepState _state;
        private readonly ISceneHost _scene;
        private readonly ISerializer _serializer;

        public LockstepController(LockstepState state, ISceneHost scene, ISerializer serializer)
        {
            _state = state;
            _scene = scene;
            _serializer = serializer;
        }

        protected override Task OnConnected(IScenePeerClient peer)
        {
            var updateId = 0;
            Player newPlayer;

            PlayersUpdateCommand addCommand;
            lock (_state.SyncRoot)
            {

                updateId = _state.CurrentUpdateId + 1;

                newPlayer = new Player { Peer = peer, Id = _state.NextPlayerId };

                _scene.Send(peer.MatchPeerFilter, "lockstepPlayers.installSnapshot", static (s, ctx) =>
                {
                    var (serializer, state, playerId) = ctx;
                    serializer.Serialize(state.GetSnapshot(playerId), s);
                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (_serializer, _state, newPlayer.Id));

                _state.Players[peer.SessionId] = newPlayer;
                _state.CurrentUpdateId = updateId;
                _state.NextPlayerId++;

            }
            addCommand = new PlayersUpdateCommand
            {
                CommandType = PlayersUpdateCommandType.Add,
                PlayerId = newPlayer.Id,
                PlayerSessionId = newPlayer.Peer.SessionId,
                UpdateId = updateId
            };

            _scene.Send(_scene.MatchAllFilter, "lockstepPlayers.update", static (s, state) =>
            {
                var (serializer, addCommand) = state;
                serializer.Serialize(addCommand, s);
            }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (_serializer, addCommand));

            return Task.CompletedTask;
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            var updateId = 0;
            Player? player;
            lock (_state.SyncRoot)
            {

                if (_state.Players.Remove(args.Peer.SessionId, out player))
                {
                    updateId = _state.CurrentUpdateId + 1;
                    _state.CurrentUpdateId = updateId;
                }
            }
            if (player != null)
            {
                _scene.Send(_scene.MatchAllFilter, "lockstep.playersUpdate", static (s, state) =>
                {
                    var (serializer, player, updateId) = state;
                    serializer.Serialize(new PlayersUpdateCommand
                    {
                        CommandType = PlayersUpdateCommandType.Remove,
                        PlayerId = player.Id,
                        PlayerSessionId = player.Peer.SessionId,
                        UpdateId = updateId
                    }, s);
                }, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (_serializer, player, updateId));
            }

            return Task.CompletedTask;
        }

    }
    /// <summary>
    /// Types of commands
    /// </summary>
    public enum PlayersUpdateCommandType
    {
        /// <summary>
        /// Adds a player
        /// </summary>
        Add,
        /// <summary>
        /// Removes a player
        /// </summary>
        Remove
    }

    /// <summary>
    /// Player update command
    /// </summary>
    [MessagePackObject]
    public class PlayersUpdateCommand
    {
        /// <summary>
        /// Gets the type of the command.
        /// </summary>
        [Key(0)]
        public required PlayersUpdateCommandType CommandType { get; init; }

        /// <summary>
        /// Gets the update id.
        /// </summary>
        [Key(1)]
        public required int UpdateId { get; init; }

        /// <summary>
        /// Gets the id of the player concerned by the command.
        /// </summary>
        [Key(2)]
        public required int PlayerId { get; init; }

        /// <summary>
        /// Gets the <see cref="SessionId"/> of the player concerned by the command.
        /// </summary>
        [Key(3)]
        public required SessionId PlayerSessionId { get; init; }
    }


    /// <summary>
    /// Command that installs a snapshot.
    /// </summary>
    [MessagePackObject]
    public class PlayersSnapshotInstallCommand
    {
        /// <summary>
        /// Gets the id of the state saved in the snapshot.
        /// </summary>
        [Key(0)]
        public required int UpdateId { get; init; }

        /// <summary>
        /// Gets the id of the current player.
        /// </summary>
        [Key(1)]
        public required int CurrentPlayerId { get; init; }

        /// <summary>
        /// Gets the state stored in the snapshot.
        /// </summary>
        [Key(2)]
        public required Dictionary<int, SessionId> Players { get; init; }
    }

}
