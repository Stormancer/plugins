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
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// This class represents an open game session that can accept players from the GameFinder.
    /// </summary>
    public class OpenGameSession
    {
        /// <summary>
        /// The scene Id of the game session.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Game-specific data for the game session.
        /// </summary>
        public JObject Data { get; set; }

        /// <summary>
        /// Whether this game session still accepts players.
        /// </summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>
        /// Close this game session, marking it to be removed from the game finder.
        /// </summary>
        public void Close()
        {
            IsOpen = false;
        }

        /// <summary>
        /// How many game finder passes this game session has been open for.
        /// </summary>
        public int NumGameFinderPasses { get; internal set; } = 0;

        /// <summary>
        /// The time at which this game session was opened.
        /// </summary>
        public DateTime CreationTimeUtc { get; internal set; } = DateTime.UtcNow;


        private IObserver<IEnumerable<Team>> observer;
        /// <summary>
        /// Notify the game session about new incoming teams.
        /// </summary>
        /// <remarks>
        /// Game sessions that are not public have a list of authorized teams.
        /// Thus it is required to add new members to this list before they connect to the game session.
        /// </remarks>
        /// <param name="teams">Teams that have been added to the game session by the GameFinder.</param>
        /// <returns></returns>
        internal async Task RegisterTeams(IEnumerable<Team> teams)
        {
            observer.OnNext(teams);
            //await requestContext.SendValue(stream => requestContext.RemotePeer.Serializer().Serialize(teams, stream));
        }

        internal void Complete()
        {
            observer.OnCompleted();
        }

        internal OpenGameSession(string origin, JObject data, IObserver<IEnumerable<Team>> observer)
        {
            SceneId = origin;
            Data = data;
            this.observer = observer;
        }
    }
}
