using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Extension methods for the quick queue game finder implementation.
    /// </summary>
    public static class QuickQueueExtensions
    {
        /// <summary>
        /// Configures a simple quick queue matchmaker.
        /// </summary>
        /// <param name="gameFinderConfig"></param>
        /// <param name="optionsBuilder"></param>
        /// <returns></returns>
        public static GameFinderConfig ConfigureQuickQueue<TPartySettings>(this GameFinderConfig gameFinderConfig, Func<QuickQueueOptions<TPartySettings>, QuickQueueOptions<TPartySettings>> optionsBuilder)
        {
            gameFinderConfig.ConfigureDependencies(dep =>
            {
                dep.Register<QuickQueueGameFinder<TPartySettings>>().As<IGameFinderAlgorithm>().InstancePerRequest();
                dep.Register<QuickQueueGameFinderResolver>().As<IGameFinderResolver>().InstancePerRequest();
            });


            var options = optionsBuilder(new QuickQueueOptions<TPartySettings>());

            OptionsStore[gameFinderConfig.ConfigId] = options;

            return gameFinderConfig;
        }

        private static Dictionary<string, object> OptionsStore = new Dictionary<string, object>();

        /// <summary>
        /// Gets options from the quick queue OptionsStore.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static T GetOptions<T>(string id)
        {
            lock (OptionsStore)
            {
                return (T)OptionsStore[id];
            }
        }
        /// <summary>
        /// Configures a simple quick queue matchmaker.
        /// </summary>
        /// <param name="gameFinderConfig"></param>
        /// <param name="optionsBuilder"></param>
        /// <returns></returns>
        public static GameFinderConfig ConfigureQuickQueue(this GameFinderConfig gameFinderConfig, Func<QuickQueueOptions, QuickQueueOptions> optionsBuilder)
        {
            gameFinderConfig.ConfigureDependencies(dep =>
            {
                dep.Register<QuickQueueGameFinder>().As<IGameFinderAlgorithm>().InstancePerRequest();
                dep.Register<QuickQueueGameFinderResolver>().As<IGameFinderResolver>().InstancePerRequest();
            });

            gameFinderConfig.ConfigureOnCreatingScene(scene =>
            {
                lock (OptionsStore)
                {
                    if (!OptionsStore.ContainsKey(gameFinderConfig.ConfigId))
                    {
                        var options = optionsBuilder(new QuickQueueOptions());
                        OptionsStore[gameFinderConfig.ConfigId] = options;
                    }
                }
                //var config = scene.DependencyResolver.Resolve<IConfiguration>();
                //config.SetDefaultValue($"gamefinder.configs.{gameFinderConfig.ConfigId}", options);
            });


            return gameFinderConfig;
        }

    }

    /// <summary>
    /// Base class for Quickqueue option objects, contains common options.
    /// </summary>
    public class QuickQueueOptionsBase
    {
        /// <summary>
        /// Id of the template the game finder should use to create game session scenes.
        /// </summary>
        public string gameSessionTemplate { get; set; } = "gameSession";
    }
    /// <summary>
    /// Options for the quick queue game finder implementation.
    /// </summary>
    public class QuickQueueOptions<TPartySettings> : QuickQueueOptionsBase
    {
        /// <summary>
        /// Size of the teams.
        /// </summary>
        public Func<IDependencyResolver, Party, TPartySettings?, uint> teamSize { get; set; } = (_, _, _) => 1;

        /// <summary>
        /// Number of teams in a game.
        /// </summary>
        public Func<IDependencyResolver, Party, TPartySettings?, uint> teamCount { get; set; } = (_, _, _) => 2;

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public Func<IDependencyResolver, Party, TPartySettings?, bool> allowJoinExistingGame = (_, _, _) => false;

        /// <summary>
        /// Returns true if 2 party can play together.
        /// </summary>
        public Func<IDependencyResolver, Party, TPartySettings?, Party, TPartySettings?, bool> canPlayTogether { get; set; } = (_, _, _, _, _) => true;

        /// <summary>
        /// Customizes the games created by the game finder.
        /// </summary>
        public Action<IDependencyResolver, QuickQueueGameSessionConfig, NewGame> onCreatingGame { get; set; } = (_, _, _) => { };

        /// <summary>
        /// party parameters factory method.
        /// </summary>
        public Func<IDependencyResolver, Party, Task<TPartySettings?>> GetSettings { get; set; } = (dr, p) =>
        {
            if (p.CustomData.Length > 0)
            {
                try
                {

                    return Task.FromResult(dr.Resolve<ISerializer>().Deserialize<TPartySettings?>(new MemoryStream(p.CustomData)));
                }
                catch (Exception)
                {
                }
            }
            return Task.FromResult<TPartySettings?>(default);
        };

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public QuickQueueOptions<TPartySettings> AllowJoinExistingGame(bool allow)
        {
            return AllowJoinExistingGame((_, _, _) => allow);
        }

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public QuickQueueOptions<TPartySettings> AllowJoinExistingGame(Func<IDependencyResolver, Party, TPartySettings?, bool> func)
        {
            allowJoinExistingGame = func;
            return this;
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamSize(uint size)
        {

            return TeamSize((_, _, _) => size);
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="teams"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamCount(uint teams)
        {
            return TeamCount((_, _, _) => teams);
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="getTeamSize"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamSize(Func<IDependencyResolver, Party, TPartySettings?, uint> getTeamSize)
        {
            teamSize = getTeamSize;
            return this;
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="getTeams"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamCount(Func<IDependencyResolver, Party, TPartySettings?, uint> getTeams)
        {
            teamCount = getTeams;
            return this;
        }

        /// <summary>
        /// Filters who can play together.
        /// </summary>
        /// <param name="canPlay"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> CanPlay(Func<IDependencyResolver, Party, TPartySettings?, Party, TPartySettings?, bool> canPlay)
        {
            canPlayTogether = canPlay;
            return this;
        }

        /// <summary>
        /// Sets a function creating settings from a party.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> SettingsGetter(Func<IDependencyResolver, Party, Task<TPartySettings?>> value)
        {
            GetSettings = value;
            return this;
        }

        /// <summary>
        /// Sets the template to use to create gamesessions.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> GameSessionTemplate(string template)
        {
            gameSessionTemplate = template;
            return this;
        }

        /// <summary>
        /// Customize game sessions created by the quick queue matchmaker.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> OnCreatingGame(Action<IDependencyResolver, QuickQueueGameSessionConfig, NewGame> action)
        {
            onCreatingGame = action;
            return this;
        }
    }


    /// <summary>
    /// Options for the quick queue game finder implementation.
    /// </summary>
    public class QuickQueueOptions : QuickQueueOptionsBase
    {
        /// <summary>
        /// Size of the teams.
        /// </summary>
        public Func<IDependencyResolver, Party, uint> teamSize { get; set; } = (_, _) => 1;

        /// <summary>
        /// Number of teams in a game.
        /// </summary>
        public Func<IDependencyResolver, Party, uint> teamCount { get; set; } = (_, _) => 2;

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public Func<IDependencyResolver, Party, bool> allowJoinExistingGame = (_, _) => false;

        /// <summary>
        /// Can parties play together method.
        /// </summary>
        public Func<IDependencyResolver, Party, Party, bool> CanPlayTogether { get; set; } = (_, _, _) => true;

        /// <summary>
        /// Triggered when a game is created by the quick queue.
        /// </summary>
        public Action<IDependencyResolver, QuickQueueGameSessionConfig, NewGame> onCreatingGame { get; set; } = (_, _, _) => { };

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public QuickQueueOptions AllowJoinExistingGame(bool allow)
        {
            return AllowJoinExistingGame((_, _) => allow);
        }

        /// <summary>
        /// Allows joining a game already in progress.
        /// </summary>
        /// <remarks>
        /// If true, the game is created when the first player enters gamefinding, then new players are added until the game is full.
        /// </remarks>
        public QuickQueueOptions AllowJoinExistingGame(Func<IDependencyResolver, Party, bool> func)
        {
            allowJoinExistingGame = func;
            return this;
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamSize(uint size)
        {

            return TeamSize((_, _) => size);
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="teams"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamCount(uint teams)
        {
            return TeamCount((_, _) => teams);
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="getTeamSize"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamSize(Func<IDependencyResolver, Party, uint> getTeamSize)
        {
            teamSize = getTeamSize;
            return this;
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="getTeams"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamCount(Func<IDependencyResolver, Party, uint> getTeams)
        {
            teamCount = getTeams;
            return this;
        }

        /// <summary>
        /// Filters who can play together.
        /// </summary>
        /// <param name="canPlay"></param>
        /// <returns></returns>
        public QuickQueueOptions CanPlay(Func<IDependencyResolver, Party, Party, bool> canPlay)
        {
            CanPlayTogether = canPlay;
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
