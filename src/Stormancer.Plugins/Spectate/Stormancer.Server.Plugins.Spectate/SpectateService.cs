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

using Microsoft.IO;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Spectate
{
    internal class SpectateService : ISpectateService
    {
        private readonly ISpectateRepository _spectateRepository;
        public readonly IUserSessions _userSessions;
        private readonly ISerializer _serializer;
        private readonly ISceneHost scene;
        private readonly RecyclableMemoryStreamProvider _memoryStreamProvider;

        public SpectateService(ISpectateRepository spectateRepository, IUserSessions userSessions, ISerializer serializer, ISceneHost scene,RecyclableMemoryStreamProvider memoryStreamProvider)
        {
            _spectateRepository = spectateRepository;
            _userSessions = userSessions;
            _serializer = serializer;
            this.scene = scene;
            _memoryStreamProvider = memoryStreamProvider;
        }

        public Task SendFrames(IEnumerable<Frame> frames)
        {
            BroadcastFrames(frames);
            _spectateRepository.AddFrames(frames);
            return Task.CompletedTask;
        }


        public Task<IEnumerable<FrameList>> GetFrames(ulong startTime, ulong endTime)
        {
            return Task.FromResult(_spectateRepository.GetFrames(startTime, endTime));
        }

        public ulong SubscribeToFrames(RequestContext<IScenePeerClient> request)
        {
            
            _spectateRepository.SubscribeToFrames( request);
            var frame = _spectateRepository.LastFrame;
            if (frame != null)
            {
                return frame.Time;
            }
            else
            {
                return 0;
            }

        }
        private Task BroadcastFrames(IEnumerable<Frame> frames)
        {
            using (var memStream = _memoryStreamProvider.GetStream())
            {
                _serializer.Serialize(frames, (IBufferWriter<byte>)memStream);
                memStream.Seek(0, SeekOrigin.Begin);

                var tasks = new List<Task>();
                var requests = _spectateRepository.GetSubscribers();
                return scene.Send(requests, "Spectate.SendFrames", s => memStream.CopyTo(s), PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED);
               
            
            }
        }

        public void Unsubscribe(SessionId sessionId)
        {
            _spectateRepository.UnsubscribeFromFrames(sessionId);
        }

    }
}
