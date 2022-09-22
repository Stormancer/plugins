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
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Spectate
{
    public class SpectateController : ControllerBase
    {
        private readonly ISpectateService _spectateService;
        private readonly IGameSessionService _gameSessionService;

        public SpectateController(ISpectateService spectateService, IGameSessionService gameSessionService)
        {
            _spectateService = spectateService;
            _gameSessionService = gameSessionService;
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            _spectateService.Unsubscribe(args.Peer.SessionId);

            return Task.CompletedTask;
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task SendFrames(IEnumerable<FrameDataDto> frames)
        {
            var sessionId = this.Request.RemotePeer.SessionId;
            var sessionIdStr = sessionId.ToString();

            var cfg = _gameSessionService.GetGameSessionConfig();
            if (!cfg.Teams.Any(t => t.AllPlayers.Any(p => p.SessionId == sessionIdStr)))
            {
                throw new InvalidOperationException("User not found in GameSession");
            }

            return _spectateService.SendFrames(frames.Select(f => new Frame { Type = f.Type, Time = f.Time, data = f.data, Origin = sessionId }));
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public Task<IEnumerable<FrameList>> GetFrames(ulong startTime, ulong endTime)
        {
            return _spectateService.GetFrames(startTime, endTime);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public ulong SubscribeToFrames()
        {
            return _spectateService.SubscribeToFrames(this.Request);
        }
    }
}
