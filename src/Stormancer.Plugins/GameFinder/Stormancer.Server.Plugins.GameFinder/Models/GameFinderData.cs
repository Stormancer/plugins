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

using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Concurrent;

namespace Stormancer.Server.Plugins.GameFinder
{
    internal class GameFinderData
    {
        public ConcurrentDictionary<Party, GameFinderRequestState> waitingParties { get; } = new ConcurrentDictionary<Party, GameFinderRequestState>();
        public ConcurrentDictionary<SessionId, Party> peersToGroup { get; } = new ConcurrentDictionary<SessionId, Party>();

        public ConcurrentDictionary<string, OpenGameSession> openGameSessions { get; } = new ConcurrentDictionary<string, OpenGameSession>();

        public bool IsRunning { get; set; }
        public string kind { get; set; }
        public TimeSpan interval { get; set; }
        public bool isReadyCheckEnabled { get; set; }
        public int readyCheckTimeout { get; set; }
        public bool acceptRequests { get; set; } = true;
    }
}
