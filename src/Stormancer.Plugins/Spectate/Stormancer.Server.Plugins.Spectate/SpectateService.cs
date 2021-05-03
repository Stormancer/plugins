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

using Stormancer.Plugins;
using Stormancer.Server.Plugins.Users;
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

        public SpectateService(ISpectateRepository spectateRepository, IUserSessions userSessions, ISerializer serializer)
        {
            _spectateRepository = spectateRepository;
            _userSessions = userSessions;
            _serializer = serializer;
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

        public async Task SubscribeToFrames(CancellationToken ct, string sessionId, RequestContext<IScenePeerClient> request)
        {
            _spectateRepository.SubscribeToFrames(sessionId, request);
            var tcs = new TaskCompletionSource<bool>();
            using var registration = ct.Register(() =>
            {
                _spectateRepository.UnsubscribeFromFrames(sessionId);
                tcs.SetResult(true);
            });
            await tcs.Task;
        }

        private Task BroadcastFrames(IEnumerable<Frame> frames)
        {
            using (var stream = new MemoryStream())
            {
                _serializer.Serialize(frames, stream);
                var data = stream.ToArray();

                var tasks = new List<Task>();
                var requests = _spectateRepository.GetSubscribers();
                foreach (var request in requests)
                {
                    var task = request.SendValue(stream =>
                    {
                        stream.Write(data);
                    });
                    tasks.Add(task);
                }

                return Task.WhenAll(tasks);
            }
        }
    }
}
