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
using Stormancer.Server.Plugins.GameSession.Models;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    /// <summary>
    /// Provides functions controlling the game session hosted on the scene.
    /// </summary>
    public interface IGameSessionService
    {
        /// <summary>
        /// Returns the id of the gamesession.
        /// </summary>
        string GameSessionId { get; }

        /// <summary>
        /// Gets the UTC Date the game session was created.
        /// </summary>
        DateTime OnCreated { get; }

        void SetConfiguration(dynamic metadata);

        /// <summary>
        /// Posts gameresults.
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="remotePeer"></param>
        /// <returns></returns>
        Task<Action<Stream,ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer);


        /// <summary>
        /// Updates the policy used to decide when the session should be shutdown.
        /// </summary>
        /// <param name="shutdown"></param>
        /// <returns></returns>
        Task UpdateShutdownMode(ShutdownModeParameters shutdown);

        /// <summary>
        /// Resets the gamesession.
        /// </summary>
        /// <returns></returns>
        Task Reset();

        /// <summary>
        /// Gets the gamesession configuration.
        /// </summary>
        /// <returns></returns>
        GameSessionConfigurationDto? GetGameSessionConfig();

        /// <summary>
        /// Create a P2P token to connect to the gamesession's host.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task<HostInfosMessage> CreateP2PToken(SessionId sessionId);

        /// <summary>
        /// Performs an update action on the gamesession config.
        /// </summary>
        /// <param name="gameSessionConfigUpdater"></param>
        void UpdateGameSessionConfig(Action<GameSessionConfiguration> gameSessionConfigUpdater);

        /// <summary>
        /// Tries to start the gamesession.
        /// </summary>
        /// <returns></returns>
        Task<bool> TryStart();

        /// <summary>
        /// Sets a peer as ready in the gamesession.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="customData"></param>
        /// <returns></returns>
        Task SetPlayerReady(IScenePeerClient peer,string customData);

        /// <summary>
        /// Disconnects the peer from the gameSession
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task OnPeerDisconnecting(IScenePeerClient peer);

        /// <summary>
        /// The peer is disconnecting from the gamesession.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task OnPeerConnecting(IScenePeerClient peer);

        /// <summary>
        /// The peer connection was rejected by the gamesession.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task OnPeerConnectionRejected(IScenePeerClient peer);

        /// <summary>
        /// The peer successfully connected to the gamesession.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task OnPeerConnected(IScenePeerClient peer);

        /// <summary>
        /// Sets a peer as faulted.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        Task SetPeerFaulted(IScenePeerClient peer);

        /// <summary>
        /// Is host
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        bool IsHost(SessionId sessionId);


        /// <summary>
        /// Event fired when the game session completes or is destroyed.
        /// </summary>
        event Action OnGameSessionCompleted;

        /// <summary>
        /// Creates a reservation
        /// </summary>
        /// <param name="team"></param>
        /// <param name="args"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>Returns null if no reservation was done.</remarks>
        /// <returns></returns>
        Task<GameSessionReservation?> CreateReservationAsync(Team team, JObject args, CancellationToken cancellationToken);

        /// <summary>
        /// Cancels a reservation using its id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CancelReservationAsync(string id, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a P2P token to connect 2 players of the game session together.
        /// </summary>
        /// <param name="callerSessionId"></param>
        /// <param name="remotePeerSessionId"></param>
        /// <returns></returns>
        Task<string> CreateP2PToken(SessionId callerSessionId, SessionId remotePeerSessionId);


        /// <summary>
        /// Dimensions used to group game sessions for analytics purpose.
        /// </summary>
        public IReadOnlyDictionary<string,string> Dimensions { get; }

        /// <summary>
        /// Sets the value of a dimension.
        /// </summary>
        /// <param name="dimension"></param>
        /// <param name="value"></param>
        public void SetDimension(string dimension, string value);

        /// <summary>
        /// Returns realtime information about the game session.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<InspectLiveGameSessionResult> InspectAsync(CancellationToken cancellationToken);
    }
}
