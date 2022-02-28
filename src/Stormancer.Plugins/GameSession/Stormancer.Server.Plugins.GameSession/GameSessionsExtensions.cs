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
using Stormancer.Server;
using Stormancer.Server.Plugins.GameSession;
using System;
using System.Collections.Generic;

namespace Stormancer
{
    /// <summary>
    /// Extension methods used to configure game sessions.
    /// </summary>
    public static class GameSessionsExtensions
    {
        /// <summary>
        /// Adds gamesession capabilities to a scene.
        /// </summary>
        /// <remarks>Must be called during scene initialization, for instance in a template's factory.</remarks>
        /// <param name="scene"></param>
        public static void AddGameSession(this ISceneHost scene)
        {
            scene.Metadata[GameSessionPlugin.METADATA_KEY] = "enabled";
        }

        /// <summary>
        /// Configures a scene template as a gamesession.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="templateId"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHost ConfigureGameSession(this IHost host, string templateId, Func<GameSessionTemplateConfiguration, GameSessionTemplateConfiguration> builder)
        {
            lock (_syncRoot)
            {
                var b = GetConfig(templateId);
                builder(b);
            }

            host.AddSceneTemplate(templateId, scene =>
            {


                scene.AddGameSession();

                var b = GetConfig(scene.Template);
                b.CustomizationAction(scene);


            });

            return host;
        }


        private static object _syncRoot = new object();

        internal static GameSessionTemplateConfiguration GetConfig(string templateId)
        {
            lock (_syncRoot)
            {
                if (_gameSessionConfigs.TryGetValue(templateId, out var b))
                {
                    return b;
                }
                else
                {
                    b = new GameSessionTemplateConfiguration();
                    _gameSessionConfigs[templateId] = b;
                    return b;
                }
            }
        }

        private static Dictionary<string, GameSessionTemplateConfiguration> _gameSessionConfigs = new Dictionary<string, GameSessionTemplateConfiguration>();
    }

    /// <summary>
    /// Configures a game session server.
    /// </summary>
    public class GameSessionServerTemplateConfiguration
    {
        internal GameSessionServerTemplateConfiguration()
        {

        }

        internal Func<ISceneHost, string?> gameServerPoolIdGetter { get; set; } = _ => null;

        internal Func<ISceneHost, bool> useGameServerGetter { get; set; } = _ => false;

        internal Func<ISceneHost, TimeSpan> serverStartTimeoutGetter { get; set; } = _ => TimeSpan.FromSeconds(60);

        /// <summary>
        /// Sets the timeout before gameserver startup is considered to have failed.
        /// </summary>
        /// <param name="getter"></param>
        /// <returns></returns>
        public GameSessionServerTemplateConfiguration StartTimeout(Func<ISceneHost, TimeSpan> getter)
        {
            serverStartTimeoutGetter = getter;
            return this;
        }

        /// <summary>
        /// Sets the timeout before gameserver startup is considered to have failed.
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public GameSessionServerTemplateConfiguration StartTimeout(TimeSpan timeSpan) => StartTimeout(_ => timeSpan);

        /// <summary>
        /// Sets the pool id to use to start a game server.
        /// </summary>
        /// <remarks>
        /// return null to not use a gameserver in the gamesession.
        /// </remarks>
        /// <param name="getter"></param>
        /// <returns></returns>
        public GameSessionServerTemplateConfiguration PoolId(Func<ISceneHost,string?> getter)
        {
            useGameServerGetter = scene => getter(scene) != null;
            gameServerPoolIdGetter = getter;
            return this;
        }
        /// <summary>
        /// Sets the pool id to use to start a game server.
        /// </summary>
        /// <remarks>
        /// return null to not use a gameserver in the gamesession.
        /// </remarks>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public GameSessionServerTemplateConfiguration PoolId(string? poolId) => PoolId(_ => poolId);


    }

    /// <summary>
    /// Configuration of a gamesession.
    /// </summary>
    public class GameSessionTemplateConfiguration
    {
        internal GameSessionTemplateConfiguration()
        {
        }


        /// <summary>
        /// Configures the gameSession to use a gameserver
        /// </summary>
        /// <returns></returns>
        public GameSessionTemplateConfiguration UseGameServer(Func<GameSessionServerTemplateConfiguration, GameSessionServerTemplateConfiguration> builder)
        {
            builder(GameServerConfig);
            return this;
        }

        internal GameSessionServerTemplateConfiguration GameServerConfig { get; set; } = new GameSessionServerTemplateConfiguration();

        internal Action<ISceneHost> CustomizationAction { get; set; } = _ => { };

        /// <summary>
        /// Additional actions to perform on the scene during building.
        /// </summary>
        /// <param name="sceneBuilder"></param>
        /// <remarks>If called several times, all the builder are called.</remarks>
        /// <returns></returns>
        public GameSessionTemplateConfiguration CustomizeScene(Action<ISceneHost> sceneBuilder)
        {
            CustomizationAction += sceneBuilder;
            return this;
        }


    }

}
