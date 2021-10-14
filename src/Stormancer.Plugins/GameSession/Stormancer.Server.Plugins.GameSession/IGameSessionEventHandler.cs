// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Provides extensisibility points during game session's lifecycles.
    /// </summary>
    public interface IGameSessionEventHandler
    {
        /// <summary>
        /// Event executed when the game session is starting.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task GameSessionStarting(GameSessionContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when the game session is started.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task GameSessionStarted(GameSessionStartedCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when  the game session is complete.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task GameSessionCompleted(GameSessionCompleteCtx ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when a client player has connected to the game session.
        /// </summary>
        /// <param name="ctx">Object containing information about the event</param>
        /// <returns></returns>
        Task OnClientConnected(ClientConnectedContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when a client is leaving the game session.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnClientLeaving(ClientLeavingContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Eent executed when a client is ready in the gamesession.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnClientReady(ClientReadyContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when a new reservation is requested.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnCreatingReservation(CreatingReservationContext ctx) => Task.CompletedTask;

        /// <summary>
        /// Event executed when a reservation is cancelled.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        Task OnReservationCancelled(ReservationCancelledContext ctx) => Task.CompletedTask;
    }

    /// <summary>
    /// Ctx
    /// </summary>
    public class ReservationCancelledContext
    {
        internal ReservationCancelledContext(Guid reservationId, IEnumerable<string> userIdsReservationsCancelled )
        {
            ReservationId = reservationId;
            UserIdsReservationsCancelled = userIdsReservationsCancelled;
        }

        /// <summary>
        /// Id of the reservation.
        /// </summary>
        public Guid ReservationId { get; }

        /// <summary>
        /// List of user ids whose reservations where actually cancelled (ie if the user connected, they won't be in the list.)
        /// </summary>
        public IEnumerable<string> UserIdsReservationsCancelled { get; }
    }

    /// <summary>
    /// Context passed to <see cref="IGameSessionEventHandler.OnCreatingReservation(CreatingReservationContext)"/>
    /// </summary>
    public class CreatingReservationContext
    {
        internal CreatingReservationContext(Team team, JObject customData, Guid reservationId)
        {
            Team = team;
            CustomData = customData;
            ReservationId = reservationId;
        }
        /// <summary>
        /// Team object created by the reservation.
        /// </summary>
        /// <remarks>
        /// If the team already exists, content are automatically merged.
        /// </remarks>
        public Team Team { get; }

        /// <summary>
        /// Reservation custom data.
        /// </summary>
        public JObject CustomData { get; }

        /// <summary>
        /// Id of the reservation
        /// </summary>
        public Guid ReservationId { get; }

        /// <summary>
        /// True if accept the reservation.
        /// </summary>
        /// <remarks>
        /// true by default.
        /// </remarks>
        public bool Accept { get; set; } = true;
    }

    /// <summary>
    /// Base context for game session events.
    /// </summary>
    public class GameSessionContext
    {
        internal GameSessionContext(ISceneHost scene, GameSessionConfiguration config, IGameSessionService service)
        {
            Scene = scene;
            Config = config;
            Service = service;
        }

        /// <summary>
        /// Scene running the game session.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Id of the game session scene.
        /// </summary>
        public string Id { get => Scene.Id; }

        /// <summary>
        /// Configuration of the gamesession.
        /// </summary>
        public GameSessionConfiguration Config { get; }

        /// <summary>
        /// Game session service class associated with the current gamesession.
        /// </summary>
        public IGameSessionService Service { get; }
    }

    /// <summary>
    /// Context passed to a <see cref="IGameSessionEventHandler.GameSessionStarted(GameSessionStartedCtx)"/> event.
    /// </summary>
    public class GameSessionStartedCtx : GameSessionContext
    {
        internal GameSessionStartedCtx(IGameSessionService service, ISceneHost scene, IEnumerable<PlayerPeer> peers, GameSessionConfiguration config) : base(scene, config, service)
        {
            Peers = peers;
        }

        /// <summary>
        /// Players in the game session.
        /// </summary>
        public IEnumerable<PlayerPeer> Peers { get; }
    }

    /// <summary>
    /// Context passed to a <see cref="IGameSessionEventHandler.OnClientReady(ClientReadyContext)"/> event.
    /// </summary>
    public class ClientReadyContext
    {
        internal ClientReadyContext(IScenePeerClient peer)
        {
            Peer = peer;
        }

        /// <summary>
        /// Peer that sent the ready message to the server.
        /// </summary>
        public IScenePeerClient Peer { get; }
    }

    /// <summary>
    /// Context passed to a <see cref="IGameSessionEventHandler.GameSessionCompleted(GameSessionCompleteCtx)"/> event.
    /// </summary>
    public class GameSessionCompleteCtx : GameSessionContext
    {
        internal GameSessionCompleteCtx(IGameSessionService service, ISceneHost scene, GameSessionConfiguration config, IEnumerable<GameSessionResult> results, IEnumerable<string> players) : base(scene, config, service)
        {
            Results = results;
            PlayerIds = players;
            ResultsWriter = (p, s) => { };
        }

        /// <summary>
        /// Sequence of <see cref="GameSessionResult"/> sent by the clients of the gamesession.
        /// </summary>
        public IEnumerable<GameSessionResult> Results { get; }

        /// <summary>
        /// Id of the players in the gamesession.
        /// </summary>
        public IEnumerable<string> PlayerIds { get; }

        /// <summary>
        /// A function executed to send back data to the players.
        /// </summary>
        public Action<Stream, ISerializer> ResultsWriter { get; set; }
    }

    /// <summary>
    /// Represents a game session result sent by a client.
    /// </summary>
    public class GameSessionResult
    {
        internal GameSessionResult(string userId, IScenePeerClient? client, Stream data)
        {
            Peer = client;
            Data = data;
            UserId = userId;
        }

        /// <summary>
        /// Client peer.
        /// </summary>
        public IScenePeerClient? Peer { get; }

        /// <summary>
        /// User id of the client.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Custom data sent by the client.
        /// </summary>
        public Stream Data { get; }
    }

    /// <summary>
    /// Context passed to a <see cref="IGameSessionEventHandler.OnClientConnected(ClientConnectedContext)"/> event.
    /// </summary>
    public class ClientConnectedContext
    {
        internal ClientConnectedContext(IGameSessionService service, PlayerPeer player, bool isHost)
        {
            GameSession = service;
            Player = player;
            IsHost = isHost;
        }
        /// <summary>
        /// Gets the player associated with the event.
        /// </summary>
        public PlayerPeer Player { get; }

        /// <summary>
        /// Gets a value indicating whether the client is the host of the game.
        /// </summary>
        public bool IsHost { get; }

        /// <summary>
        /// Current game session service.
        /// </summary>
        public IGameSessionService GameSession { get; }


    }

    /// <summary>
    /// Context passed to a <see cref="IGameSessionEventHandler.OnClientLeaving(ClientLeavingContext)"/> event.
    /// </summary>
    public class ClientLeavingContext
    {
        internal ClientLeavingContext(IGameSessionService service, PlayerPeer player, bool isHost)
        {
            GameSession = service;
            Player = player;
            IsHost = isHost;
        }
        /// <summary>
        /// Gets the player associated with the event.
        /// </summary>
        public PlayerPeer Player { get; }

        /// <summary>
        /// Gets a value indicating whether the client is the host of the game.
        /// </summary>
        public bool IsHost { get; }

        /// <summary>
        /// Current game session service.
        /// </summary>
        public IGameSessionService GameSession { get; }
    }
}
