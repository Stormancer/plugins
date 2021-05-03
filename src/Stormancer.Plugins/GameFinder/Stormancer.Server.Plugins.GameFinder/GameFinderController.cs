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
using Stormancer.Server.Plugins.Models;

namespace Stormancer.Server.Plugins.GameFinder
{

    /// <summary>
    /// GameFinder controller
    /// </summary>
    [Service(Named =true, ServiceType = "stormancer.plugins.gamefinder")]
    public class GameFinderController : ControllerBase, IServiceMetadataProvider
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
        /// <param name="party"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [S2SApi( Route= FindGameS2SRoute)]
        public Task FindGame(Models.Party party, IS2SRequestContext request)
        {
            return _gameFinderService.FindGame(party, request.CancellationToken);
        }

        /// <summary>
        /// Gets the matchmaking metrics.
        /// </summary>
        /// <returns></returns>
        [S2SApi]
        public Dictionary<string, int> GetMetrics()
        {
            return _gameFinderService.GetMetrics();
        }

        /// <summary>
        /// Make this game session open to new players.
        /// </summary>
        /// <param name="data">Custom data that will be passed to the gamefinder.</param>
        /// <param name="request"></param>
        /// <returns>
        /// An async enumerable that yields every Team that is added to the game session.
        /// It completes when the game session has been withdrawn from the gamefinder.
        /// </returns>
        /// <remarks>
        /// When this method is called, the game session will be made available to the GameFinder.
        /// From there, players in the target gamefinder will be able to join it, provided  the gamefinder supports it.
        /// The game session stays open until the request is canceled.
        /// </remarks>
        [S2SApi]
        public IAsyncEnumerable<IEnumerable<Team>> OpenGameSession(JObject data,[S2SContextUsage(S2SRequestContextUsage.None)] IS2SRequestContext request)
        {
            return _gameFinderService.OpenGameSession(data, request);
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
        /// Cancels matchmaking
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        [Api(ApiAccess.Public, ApiType.FireForget, Route = "gamefinder.cancel")]
        public Task CancelSearchFromClient(Packet<IScenePeerClient> packet)
        {
            return _gameFinderService.CancelGame(packet.Connection, true);
        }


        /// <summary>
        /// Cancels matchmaking when the peer disconnects.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected override Task OnDisconnected(DisconnectedArgs args) => _gameFinderService.CancelGame(args.Peer, false);

     
        string IServiceMetadataProvider.GetServiceInstanceId(ISceneHost scene)
        {
            return scene.Id;
        }
    }
}
