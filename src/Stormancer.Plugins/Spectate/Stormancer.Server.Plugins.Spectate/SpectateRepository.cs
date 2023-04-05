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

using Nest;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.Spectate
{
    public class SpectateRepository : ISpectateRepository
    {
        //private readonly SortedSet<List<Frame>> _frames = new SortedSet<List<Frame>>(new FrameComparer());

        private readonly SortedSet<FrameList> _frames = new SortedSet<FrameList>(new FrameListComparer());

        private const int _cacheSize = 60;

        private readonly FrameList[] _cacheFrames = new FrameList[_cacheSize];

        private ulong _cacheLastTime = 0;

        private readonly HashSet<SessionId> _requests = new HashSet<SessionId>();

        public FrameList? LastFrame
        {
            get
            {
                lock (_frames) { return _frames.Max; }
            }
        }

        private class FrameListComparer : IComparer<FrameList>
        {
            public int Compare(FrameList? x, FrameList? y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                if (x == null)
                {
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                return x.Time.CompareTo(y.Time);
            }
        }

        //public void AddFrames(IEnumerable<Frame> frames)
        //{
        //    lock (_frames)
        //    {
        //        _frames.UnionWith(frames);
        //    }
        //}

        //public IEnumerable<Frame> GetFrames(ulong startTime, ulong endTime)
        //{
        //    lock (_frames)
        //    {
        //        return _frames.GetViewBetween(new Frame { Time = startTime }, new Frame { Time = endTime });
        //    }
        //}

        public void AddFrames(IEnumerable<Frame> frames)
        {
            lock (_frames)
            {
                //_frames.UnionWith(frames);
                foreach (var frame in frames)
                {
                    FrameList frameList = null;

                    if (_cacheLastTime < _cacheSize || (frame.Time > (_cacheLastTime - _cacheSize) && frame.Time <= _cacheLastTime))
                    {
                        var index = frame.Time % _cacheSize;
                        frameList = _cacheFrames[index];
                    }

                    if (frameList == null || frameList.Time != frame.Time)
                    {
                        if (!_frames.TryGetValue(new FrameList { Time = frame.Time }, out frameList))
                        {
                            frameList = new FrameList { Time = frame.Time, Frames = new List<Frame>() };

                            _frames.Add(frameList);

                            var index = frame.Time % _cacheSize;
                            _cacheFrames[index] = frameList;

                            if (_cacheLastTime < frame.Time)
                            {
                                _cacheLastTime = frame.Time;
                            }
                        }
                    }

                    if (frameList == null)
                    {
                        throw new InvalidOperationException("FrameList not found");
                    }

                    frameList.Frames.Add(frame);
                }
            }
        }

        public IEnumerable<FrameList> GetFrames(ulong startTime, ulong endTime)
        {
            lock (_frames)
            {
                return _frames.GetViewBetween(new FrameList { Time = startTime }, new FrameList { Time = endTime }).ToArray();
            }
        }

        public bool SubscribeToFrames(RequestContext<IScenePeerClient> request)
        {
            var sessionId = request.RemotePeer.SessionId;
            lock (_requests)
            {
               
                var result = _requests.Add(sessionId);
                if (result)
                {
                    _filter = new MatchArrayFilter(_requests);
                }
                return result;

            }
        }

        public void UnsubscribeFromFrames(SessionId sessionId)
        {
            lock (_requests)
            {
                if(_requests.Remove(sessionId))
                {
                    _filter = new MatchArrayFilter(_requests);
                }
             
            }
        }

        private MatchArrayFilter? _filter;
        public MatchArrayFilter? GetSubscribers()
        {

            return _filter;

        }
    }
}
