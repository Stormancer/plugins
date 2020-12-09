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
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    ///  GameFinder plugin
    /// </summary>
    public class GameFinderPlugin : IHostPlugin
    {
        /// <summary>
        /// Scene metadata key
        /// </summary>
        public const string METADATA_KEY = "stormancer.plugins.gamefinder";

        /// <summary>
        /// Key of the protocol version metadata entry.
        /// </summary>
        public const string ProtocolVersionKey = "stormancer.plugins.gamefinder.protocol";

        internal static Dictionary<string, GameFinderConfig> Configs = new Dictionary<string, GameFinderConfig>();

        /// <summary>
        /// Build the plugin (register components in the IoC)
        /// </summary>
        /// <param name="ctx"></param>
        public void Build(HostPluginBuildContext ctx)
        {
            //ctx.HostStarting += HostStarting;
            ctx.SceneDependenciesRegistration += SceneDependenciesRegistration;
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<GameFinderController>();
                builder.Register<GameFinderData>().AsSelf().InstancePerScene();
                builder.Register<GameFinderProxy>().As<IGameFinder>();
                builder.Register<ServiceLocationProvider>().As<IServiceLocatorProvider>();
            };
            ctx.SceneCreated += SceneCreated;



            ctx.HostStarted += (IHost host) =>
            {
                var logger = host.DependencyResolver.Resolve<ILogger>();
                logger.Log(LogLevel.Info, "gamefinder", "Creating gamefinder scenes.", new { });

                if (host.Metadata.TryGetValue("gamefinder.ids", out var ids))
                {

                    foreach (var id in ids.Split(','))
                    {
                        logger.Log(LogLevel.Info, "gamefinder", "Ensure scene '{id}' is created.", new { });
                        host.EnsureSceneExists(id, id, false, true);
                    }
                }
            };
        }



        private void SceneDependenciesRegistration(IDependencyBuilder builder, ISceneHost scene)
        {
            if (scene.Metadata.TryGetValue(METADATA_KEY, out var gameFinderType))
            {
                if (Configs.TryGetValue(scene.Id, out var config))
                {
                    builder.Register<GameFinderService>().As<IGameFinderService>().As<IConfigurationChangedEventHandler>().InstancePerScene();

                    config.RegisterDependencies(builder);
                }
            }
        }

        private void SceneCreated(ISceneHost scene)
        {
            if (scene.Metadata.TryGetValue(METADATA_KEY, out var gameFinderType))
            {
                scene.AddController<GameFinderController>();
                if (Configs.TryGetValue(gameFinderType, out var config))
                {
                    config.RunOnCreatingScene(scene);
                }
                var logger = scene.DependencyResolver.Resolve<ILogger>();
                try
                {
                    var gameFinderService = scene.DependencyResolver.Resolve<IGameFinderService>();

                    //Start gameFinder
                    scene.RunTask(gameFinderService.Run);

                }
                catch (Exception ex)
                {
                    logger.Log(LogLevel.Error, "plugins.gameFinder", $"An exception occured when creating scene {scene.Id}.", ex);
                    throw;
                }
            }
        }
    }
}
