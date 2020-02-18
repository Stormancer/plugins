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
using Stormancer.Server.Plugins.GameFinder.Models;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    public interface IGameFinderResolver
    {
        Task PrepareGameResolution(GameFinderResult gameFinderResult);

        Task ResolveGame(IGameResolverContext gameCtx);

        /// <summary>
        /// This method is called when a ticket to join an open game session is issued.
        /// </summary>
        /// <remarks>
        /// Override this method if you need to perform special actions and/or send custom data to the clients who are about to join the game.
        /// </remarks>
        /// <param name="joinGameCtx">Object containing information about the ticket.</param>
        /// <returns></returns>
        Task ResolveJoinOpenGame(JoinOpenGameContext joinGameCtx)
        {
            return Task.CompletedTask;
        }

        void RefreshConfig(dynamic config);
    }
}
