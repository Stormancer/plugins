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
using Stormancer.Server.Plugins.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Represents a list of parties.
    /// </summary>
    public readonly struct Parties
    {
        /// <summary>
        /// Creates an instance of <see cref="Parties"/>
        /// </summary>
        /// <param name="parties"></param>
        public Parties(IEnumerable<Party> parties)
        {
            Value = parties;
        }

        /// <summary>
        /// Gets the list of parties.
        /// </summary>
        public IEnumerable<Party> Value { get; }
    }

    /// <summary>
    /// Context passed to a <see cref="IGameFinderAlgorithm"/> as part of a game finding turn.
    /// </summary>
    public interface IGameFinderContext
    {
        /// <summary>
        /// Gets the dependency scope of the current game finder scope.
        /// </summary>
        IDependencyResolver Scope { get; }

        /// <summary>
        /// Parties currently in the queue.
        /// </summary>
        List<Party> WaitingParties { get; }

        /// <summary>
        /// List of parties which failed matchmaking during this pass.
        /// </summary>
        List<(Party client, string reason)> FailedClients { get; }

        /// <summary>
        /// Sets a party as having failed matchmaking.
        /// </summary>
        /// <param name="party"></param>
        /// <param name="reason"></param>
        void SetFailed(Party party, string reason);

        /// <summary>
        /// Performs a batch test of <see cref="Parties"/> sets for compatibility.
        /// </summary>
        /// <param name="candidates"></param>
        /// <returns></returns>
        IAsyncEnumerable<bool> AreCompatibleAsync(IEnumerable<Parties> candidates);

        /// <summary>
        /// Test a set of <see cref="Parties"/> for compatibility (handles crossplay)
        /// </summary>
        /// <param name="candidate"></param>
        /// <returns></returns>
        ValueTask<bool> AreCompatibleAsync(Parties candidate);
    }

    /// <summary>
    /// Classes implementing this interface provide game finder algorithms.
    /// </summary>
    public interface IGameFinderAlgorithm
    {
        /// <summary>
        /// Called every game finder pass to compute games from the waiting players list.
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        Task<GameFinderResult> FindGames(IGameFinderContext gameFinderContext);

        /// <summary>
        /// Gets public gamefinder metrics for the clients.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, int> GetMetrics();

        /// <summary>
        /// Gets private gamefinder metrics stored in the analytics system.
        /// </summary>
        /// <param name="gameFinderContext"></param>
        /// <returns></returns>
        JObject ComputeDataAnalytics(IGameFinderContext gameFinderContext);

        /// <summary>
        /// Called whenever the gamefinder config gets updated.
        /// </summary>
        /// <param name="config">the current gamefinder config section.</param>
        /// <param name="id">Id of the gamefinder config.</param>
        void RefreshConfig(string id,dynamic config);

        /// <summary>
        /// Gets the current status of the gamefinder.
        /// </summary>
        /// <param name="isAdmin"></param>
        /// <returns></returns>
        JObject GetStatus(bool isAdmin) => new JObject();
    }
}
