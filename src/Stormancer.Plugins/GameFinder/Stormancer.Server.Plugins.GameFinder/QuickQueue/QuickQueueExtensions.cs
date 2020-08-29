using Stormancer.Server.Plugins.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Extension methods for the quick queue game finder implementation.
    /// </summary>
    public static class QuickQueueExtensions
    {
        public static GameFinderConfig ConfigureQuickQueue(this GameFinderConfig gameFinderConfig, Func<QuickQueueOptions,QuickQueueOptions> optionsBuilder)
        {
            gameFinderConfig.ConfigureDependencies(dep =>
            {
                dep.Register<QuickQueueGameFinder>().As<IGameFinderAlgorithm>();
                dep.Register<QuickQueueGameFinderResolver>().As<IGameFinderResolver>();
            });

            var options = optionsBuilder(new QuickQueueOptions());
            
            var config = gameFinderConfig.Scene.DependencyResolver.Resolve<IConfiguration>();

            config.SetDefaultValue($"gameFinder.configs.{gameFinderConfig.ConfigId}", options);

            return gameFinderConfig;
        }
    }

    /// <summary>
    /// Options for the quick queue game finder implementation.
    /// </summary>
    public class QuickQueueOptions
    {
        /// <summary>
        /// Size of the teams.
        /// </summary>
        public int teamSize { get; set; } = 1;

        /// <summary>
        /// Number of teams in a game.
        /// </summary>
        public int teamCount { get; set; } = 2;

        /// <summary>
        /// Id of the template the game finder should use to create game session scenes.
        /// </summary>
        public string gameSessionTemplate { get; set; } = "gameSession";


        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamSize(int size)
        {
            teamSize = size;
            return this;
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="teams"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamCount(int teams)
        {
            teamCount = teams;
            return this;
        }

        /// <summary>
        /// Sets the template to use to create gamesessions.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        public QuickQueueOptions GameSessionTemplate(string template)
        {
            gameSessionTemplate = template;
            return this;
        }
    }
}
