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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Reason why a gamefinder search ended.
    /// </summary>
    public enum SearchEndReason
    {
        /// <summary>
        /// The search was cancelled by an user.
        /// </summary>
        Canceled = 0,

        /// <summary>
        /// An user disconnected from the gamefinder.
        /// </summary>
        Disconnected = 1,

        /// <summary>
        /// The search succeeded.
        /// </summary>
        Succeeded = 2
    }

    /// <summary>
    /// Context associated with a Gamefinder SearchStart event.
    /// </summary>
    public class SearchStartContext
    {
        /// <summary>
        /// Id of the gamefinder.
        /// </summary>
        public string GameFinderId { get; set; } = default!;

        /// <summary>
        /// Groups starting searching for a game.
        /// </summary>
        public IEnumerable<Party> Groups { get; set; } = default!;
    }

    /// <summary>
    /// Context associated with gamefinder search end events.
    /// </summary>
    public class SearchEndContext
    {
        /// <summary>
        /// Id of the gamefinder the event is related to.
        /// </summary>
        public string GameFinderId { get; set; } = default!;

        /// <summary>
        /// The party that ended the search.
        /// </summary>
        public Party Party { get; set; } = default!;

        /// <summary>
        /// The reason why the search ended.
        /// </summary>
        public SearchEndReason Reason { get; set; }

        /// <summary>
        /// The number of gamefinder loops that occured before the search ended.
        /// </summary>
        public int PassesCount { get; set; }
    }

    /// <summary>
    /// Context associated with gamefinder game started events.
    /// </summary>
    public class GameStartedContext
    {
        /// <summary>
        /// Id of the gamefinder starting the game.
        /// </summary>
        public string GameFinderId { get; set; } = default!;

        /// <summary>
        /// Description of the game starting.
        /// </summary>
        public NewGame Game { get; set; } = default!;
    }

    public class AreCompatibleContext
    {
        internal AreCompatibleContext(IEnumerable<Parties> parties)
        {
            Parties = parties.ToArray();
            Results = new bool[Parties.Length];
            for(int i = 0; i < Parties.Length; i++)
            {
                Results[i] = true;
            }
        }

        public Parties[] Parties { get; }

        public bool[] Results { get; }

        public bool GetResult(int id)
        {
            return Results[id];
        }

        public void SetResult(int id, bool result)
        {
            Results[id] = result;
        }
    }
    /// <summary>
    /// Provides implementer way to handle events in the gamefinder pipeline.
    /// </summary>
    public interface IGameFinderEventHandler
    {
        /// <summary>
        /// Fired when parties start searching for game.
        /// </summary>
        /// <param name="searchStartCtx"></param>
        /// <returns></returns>
        Task OnStart(SearchStartContext searchStartCtx) => Task.CompletedTask;

        /// <summary>
        /// Fired when a party completes a game search (successfully or not)
        /// </summary>
        /// <param name="searchEndCtx"></param>
        /// <returns></returns>
        Task OnEnd(SearchEndContext searchEndCtx) => Task.CompletedTask;

        /// <summary>
        /// Fired when the gamefinder starts a new game.
        /// </summary>
        /// <param name="searchGameStartedCtx"></param>
        /// <returns></returns>
        Task OnGameStarted(GameStartedContext searchGameStartedCtx) => Task.CompletedTask;

        /// <summary>
        /// Check if each candidate is compatible with the others in its own collection.
        /// </summary>
        /// <param name="ctx">Context containing a collection of candidates.</param>
        /// <returns></returns>
        Task AreCompatibleAsync(AreCompatibleContext ctx) => Task.CompletedTask;
    }
}
