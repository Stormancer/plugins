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

using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// GameFinder controller
    /// </summary>
    public class GameFinderController : ControllerBase
    {
        /// <summary>
        /// Find game Scene to scene route name
        /// </summary>
        public const string FindGameS2SRoute = "gamefinder.findgame";

        private readonly IGameFinderService _gameFinderService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gameFinderService"></param>
        public GameFinderController(IGameFinderService gameFinderService)
        {
            _gameFinderService = gameFinderService;
        }

        /// <summary>
        /// Start matchmaking
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Api(ApiAccess.Scene2Scene, ApiType.Rpc, Route = FindGameS2SRoute)]
        public Task FindGame(RequestContext<IScenePeer> request)
        {
            return _gameFinderService.FindGameS2S(request);
        }

        /// <summary>
        /// Gets the matchmaking metrics.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public Dictionary<string, int> GetMetrics()
        {
            return _gameFinderService.GetMetrics();
        }

        /// <summary>
        /// Open an existing game session to receive new players through this GameFinder.
        /// </summary>
        /// <param name="data">Custom data that game code will have access to in <see cref="GameFinderContext"/>.</param>
        /// <param name="request">S2S request context.</param>
        /// <returns></returns>
        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public async Task OpenGameSession(JObject data, RequestContext<IScenePeer> request)
        {
            await _gameFinderService.OpenGameSession(data, request);
        }

        /// <summary>
        /// Gets the matchmaking metrics from the client.
        /// </summary>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc, Route = "gamefinder.getmetrics")]
        public Dictionary<string, int> GetMetricsClient()
        {
            return _gameFinderService.GetMetrics();
        }

        /// <summary>
        /// Start matchmaking for a solo player without party (obsolete)
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.Rpc, Route = "gamefinder.find")]
        public Task FindGameFromClient(RequestContext<IScenePeerClient> request)
        {
            return _gameFinderService.FindGame(request);
        }

        /// <summary>
        /// Cancels matchmaking
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget, Route = "gamefinder.cancel")]
        public Task CancelSearchFromClient(Packet<IScenePeerClient> packet)
        {
            return _gameFinderService.CancelGame(packet);
        }

        /// <summary>
        /// Sets the peer as ready in the matchmaking system.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget, Route = "gamefinder.ready.resolve")]
        public Task SetClientReady(Packet<IScenePeerClient> packet)
        {
            return _gameFinderService.ResolveReadyRequest(packet);
        }

        /// <summary>
        /// Cancels matchmaking when the peer disconnects.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override Task OnDisconnected(DisconnectedArgs args) => _gameFinderService.CancelGame(args.Peer, false);
    }
}
