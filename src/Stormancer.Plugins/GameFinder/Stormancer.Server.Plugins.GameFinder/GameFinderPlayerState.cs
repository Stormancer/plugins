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

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Represents the game finder state for a player.
    /// </summary>
    public enum GameFinderPlayerState
    {
        /// <summary>
        /// The player is not currently in the game finder.
        /// </summary>
        Idle = 0,
        /// <summary>
        /// The player is currently searching for a game.
        /// </summary>
        Searching = 1,

        /// <summary>
        /// The player is connecting to a found game session.
        /// </summary>
        Connecting = 2,

        /// <summary>
        /// The player is connected to the found game session.
        /// </summary>
        Found = 3,

        /// <summary>
        /// The gamefinding process failed.
        /// </summary>
        Failed = 4,

        /// <summary>
        /// The game finding process was cancelled.
        /// </summary>
        Canceled = 5,

        /// <summary>
        /// The game finder is initializing.
        /// </summary>
        /// <remarks>
        /// The player is connecting to the game finder
        /// </remarks>
        Initializing = 6,
    }
}
