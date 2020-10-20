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

using Stormancer.Core;
using Stormancer.Server.Plugins.GameFinder;
using System;
using System.Text.RegularExpressions;

namespace Stormancer
{
    /// <summary>
    /// Provides extension methods to scenes to easily add game finder functionalities.
    /// </summary>
    public static class GameFinderServiceExtensions
    {
        /// <summary>
        /// Adds a gamefinder to the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="configId">Id of the gamefinder config. Used to locate the config section for the gamefinder in the configuration.</param>
        /// <param name="gameFinderBuilder"></param>
        public static void AddGameFinder(this ISceneHost scene, string configId, Func<GameFinderConfig, GameFinderConfig> gameFinderBuilder)
        {
            if(!Regex.IsMatch(configId, @"^[0-9a-z-_]*$", RegexOptions.IgnoreCase))
            {
                throw new ArgumentException("configId should only contain alphanumeric, dash and underscore characters.");
            }
            var config = gameFinderBuilder(new GameFinderConfig(scene,configId));
            GameFinderPlugin.Configs[scene.Id] = config;
            scene.Metadata[GameFinderPlugin.METADATA_KEY] = configId;
            scene.Metadata[GameFinderPlugin.ProtocolVersionKey] = GameFinderService.ProtocolVersion.ToString();
        }
    }
}
