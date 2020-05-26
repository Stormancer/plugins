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
        void SetConfiguration(dynamic metadata);
        Task<Action<Stream,ISerializer>> PostResults(Stream inputStream, IScenePeerClient remotePeer);
        Task UpdateShutdownMode(ShutdownModeParameters shutdown);
        Task Reset();
        GameSessionConfigurationDto GetGameSessionConfig();

        Task<string> CreateP2PToken(string sessionId);

        Task TryStart();

        bool IsHost(string sessionId);

        /// <summary>
        /// Make this game session open to new players via the specified <paramref name="gameFinder"/>.
        /// </summary>
        /// <param name="data">Custom data that will be passed to <paramref name="gameFinder"/>.</param>
        /// <param name="gameFinder">Name of the GameFinder where the session will be made available.</param>
        /// <param name="ct">When this token is canceled, the game session will be withdrawn from <paramref name="gameFinder"/>.</param>
        /// <returns>
        /// An async enumerable that yields every Team that is added to the game session by <paramref name="gameFinder"/>.
        /// It completes when the game session has been withdrawn from <paramref name="gameFinder"/>.
        /// </returns>
        /// <remarks>
        /// When this method is called, the game session will be made available to the GameFinder designated by <paramref name="gameFinder"/>.
        /// From there, players in <paramref name="gameFinder"/> will be able to join it, provided <paramref name="gameFinder"/> implements the required logic.
        /// The game session stays open until <paramref name="ct"/> is canceled.
        /// </remarks>
        IAsyncEnumerable<Team> OpenToGameFinder(JObject data, string gameFinder, CancellationToken ct);
    }
}
