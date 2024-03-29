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

using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// This class contains information about the resolution of an open game session ticket.
    /// </summary>
    public class JoinExistingGameContext
    {
        /// <summary>
        /// The ticket to the open game session.
        /// </summary>
        public ExistingGame Game { get; }
        public string GameFinderId { get; }

        internal JoinExistingGameContext(ExistingGame existingGame,string gameFinderId)
        {
            Game = existingGame;
            GameFinderId = gameFinderId;
        }

        /// <summary>
        /// An optional resolution action to be executed for the players enlisted on the <see cref="ExistingGame"/>.
        /// </summary>
        /// <remarks>
        /// Players will always be sent a connection token to the game session first, regardless of the contents of this member.
        /// </remarks>
        public Func<IGameFinderResolutionWriterContext, Task>? ResolutionAction { get; set; }
    }
}
