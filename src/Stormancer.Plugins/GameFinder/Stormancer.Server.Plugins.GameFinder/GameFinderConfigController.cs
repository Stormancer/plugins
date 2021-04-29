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

using MsgPack.Serialization;
using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.ServiceLocator;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    class GameFinderConfigController : ControllerBase
    {
        private readonly IGameFinderConfigService _gameFinderConfigService;
        private readonly RpcService rpc;
        private readonly ISerializer serializer;
        private readonly IServiceLocator locator;

        public GameFinderConfigController(
            IGameFinderConfigService gameFinderConfigService,
            RpcService rpc,
            ISerializer serializer,
            IServiceLocator locator
            )
        {
            _gameFinderConfigService = gameFinderConfigService;
            this.rpc = rpc;
            this.serializer = serializer;
            this.locator = locator;
        }



        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Dictionary<string, JObject> GetRegions()
        {
            return _gameFinderConfigService.GetRegions();
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task<List<Metrics>> GetMetrics(List<string> gameFinderIds)
        {
            var list = new List<Metrics>();
            foreach (var id in gameFinderIds)
            {
                var sceneUri = await locator.GetSceneId("stormancer.plugins.gamefinder", id);
                await foreach (var item in rpc.Rpc("GameFinder.GetMetrics", new MatchSceneFilter(sceneUri), s => { }, PacketPriority.MEDIUM_PRIORITY).Select(p =>
                {
                    using (p)
                    {
                        return serializer.Deserialize<Dictionary<string, int>>(p.Stream);
                    }
                }).ToAsyncEnumerable())
                {
                    list.Add(new Metrics { Values = item, GameFinderId = id });
                }


            }
            return list;
        }
    }

    /// <summary>
    /// Gamefinder Metrics.
    /// </summary>
    public class Metrics
    {
        /// <summary>
        /// Id of the gamefinder sending the metrics.
        /// </summary>
        [MessagePackMember(0)]
        public string GameFinderId { get; set; } = default!;

        /// <summary>
        /// Gets Metrics values.
        /// </summary>
        [MessagePackMember(1)]
        public Dictionary<string, int> Values { get; set; } = default!;
    }
}

