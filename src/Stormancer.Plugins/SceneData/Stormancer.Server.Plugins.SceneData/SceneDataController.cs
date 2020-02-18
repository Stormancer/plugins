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
using Server.Plugins.API;
using Stormancer.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SceneData
{
    class SceneDataController : ControllerBase
    {
        private readonly IEnumerable<ISceneDataProvider> _dataProviders;
        private readonly ILogger _logger;

        public SceneDataController(IEnumerable<ISceneDataProvider> dataProviders, ILogger logger)
        {
            _dataProviders = dataProviders;
            _logger = logger;
        }

        [Api(ApiAccess.Scene2Scene, ApiType.Rpc)]
        public async Task<Dictionary<string, JObject>> Get(IEnumerable<string> options)
        {
            var ctx = new GetSceneDataContext { Options = options };
            await _dataProviders.RunEventHandler(dataProvider => dataProvider.GetSceneData(ctx), ex => _logger.Log(LogLevel.Error, "SceneDataController", "GetSceneData failure in event handler processing", ex));

            return ctx.Components;
        }
    }
}
