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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{

    /// <summary>
    /// Provides Gamefinder services in a scene.
    /// </summary>
    public interface IGameFinderService
    {
        /// <summary>
        /// Starts the game finder.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task Run(CancellationToken ct);

        /// <summary>
        /// Is the game finder currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Starts the game search process for a party.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task FindGame(Party party, CancellationToken ct);

      
        /// <summary>
        /// Cancels a pending game search.
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="playerRequest"></param>
        /// <returns></returns>
        Task CancelGame(IScenePeerClient peer, bool playerRequest);

        /// <summary>
        /// Gets metrics about the game finder.
        /// </summary>
        /// <returns></returns>
        ValueTask<Dictionary<string, int>> GetMetrics();

        /// <summary>
        /// Advertises a game session in the gamefinder.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="request">Request the game session is linked with. The game session stays advertised as lon as the request is not cancelled by the game session.</param>
        /// <returns></returns>
        internal IAsyncEnumerable<IEnumerable<Team>> OpenGameSession(JObject data,IS2SRequestContext request);

        /// <summary>
        /// Check if each candidate is compatible with the others in its own collection.
        /// </summary>
        /// <param name="candidates">Collection of candidates.</param>
        /// <returns></returns>
        IAsyncEnumerable<bool> AreCompatibleAsync(IEnumerable<Parties> candidates);

        /// <summary>
        /// Check if parties are compatible with the others.
        /// </summary>
        /// <param name="candidate">Candidates.</param>
        /// <returns></returns>
        public ValueTask<bool> AreCompatibleAsync(Parties candidate)
        {
            return AreCompatibleAsync(Enumerable.Repeat(candidate, 1)).FirstAsync();
        }
    }
}
