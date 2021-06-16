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

using Stormancer.Server.Plugins.GameSession;
using Stormancer.Server.Plugins.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Simple Game finder resolver that creates gam session scenes.
    /// </summary>
    public class QuickQueueGameFinderResolver : IGameFinderResolver
    {
        private readonly IGameSessions gameSessions;
        private string template = "gameSession";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="gameSessions"></param>
        public QuickQueueGameFinderResolver(IGameSessions gameSessions)
        {
            this.gameSessions = gameSessions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameFinderResult"></param>
        /// <returns></returns>
        public Task PrepareGameResolution(GameFinderResult gameFinderResult)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="config"></param>
        public void RefreshConfig(string id, dynamic config)
        {

            var options = QuickQueueExtensions.GetOptions<QuickQueueOptionsBase>(id);

            template = options.gameSessionTemplate;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameCtx"></param>
        /// <returns></returns>
        public async Task ResolveGame(IGameResolverContext gameCtx)
        {
            if (gameCtx.Game != null)
            {
                var config = new GameSessionConfiguration();
                config.Public = false;

                foreach (var team in gameCtx.Game.Teams)
                {
                    config.Teams.Add(team);
                }

                config.Parameters = gameCtx.Game.PrivateCustomData;

                await gameSessions.Create(template, gameCtx.GameSceneId, config);

                gameCtx.ResolutionAction = (writerCtx =>
                {

                    writerCtx.WriteObjectToStream(gameCtx.Game);
                    return Task.CompletedTask;
                });
            }
        }
    }
}
