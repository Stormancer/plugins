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

using Stormancer.Server.Plugins.Models;
using System.Collections.Generic;
using System.Linq;

namespace Stormancer.Server.Plugins.GameFinder
{
    public interface IGameCandidate
    {
        /// <summary>
        /// Gets the unique id of the game.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the list of teams in the game.
        /// </summary>
        public List<Team> Teams { get; }
    }

    public static class IGameCandidateExtensions
    {
        public static IEnumerable<Party> AllParties(this IGameCandidate game) => game.Teams.SelectMany(team => team.Parties);

        public static IEnumerable<Player> AllPlayers(this IGameCandidate game) => game.Teams.SelectMany(team => team.AllPlayers);
    }
}
