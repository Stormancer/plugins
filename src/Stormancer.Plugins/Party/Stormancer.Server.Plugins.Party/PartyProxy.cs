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
using Stormancer.Server.Plugins.GameFinder;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    class PartyProxy : IPartyProxy
    {
        public readonly ISceneHost _scene;
        public readonly ISerializer _serializer;

        public PartyProxy(ISceneHost scene,
            ISerializer serializer)
        {
            _scene = scene;
            _serializer = serializer;
        }

        public async Task<Packet<IScenePeer>> FindMatch(string provider, string sceneName, GameFinderRequest gameFinderRequest, CancellationToken cancelationToken)
        {
            var rpc = _scene.DependencyResolver.Resolve<RpcService>();
            Action<Stream> writer = s => 
            {
                _serializer.Serialize(provider, s);
                _serializer.Serialize(gameFinderRequest,s);
            };
            return await rpc.Rpc(GameFinderController.FindGameS2SRoute, new MatchSceneFilter(sceneName), writer, PacketPriority.MEDIUM_PRIORITY, cancelationToken).LastOrDefaultAsync();
        }
    }
}
