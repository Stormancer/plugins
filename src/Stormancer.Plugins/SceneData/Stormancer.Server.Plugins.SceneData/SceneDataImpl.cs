﻿// MIT License
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
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.SceneData
{
    class SceneDataImpl : ISceneData
    {
        private readonly RpcService rpc;
        private readonly ISerializer serializer;

        public SceneDataImpl(RpcService rpc, ISerializer serializer)
        {
            this.rpc = rpc;
            this.serializer = serializer;
        }

        public async Task<Dictionary<string, JObject>> GetSceneData(string sceneId, IEnumerable<string> options)
        {
            var result = await rpc.Rpc("SceneData.Get", new MatchSceneFilter(sceneId), s => serializer.Serialize(options, s), PacketPriority.MEDIUM_PRIORITY).LastOrDefaultAsync();
            if (result != null)
            {
                using (result.Stream)
                {
                    return serializer.Deserialize<Dictionary<string, JObject>>(result.Stream);
                }
            }
            else
            {
                throw new InvalidOperationException("SceneData.Get returned no result.");
            }
        }
    }
}
