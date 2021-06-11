using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
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
                        OptionsStore.Add(gameFinderConfig.ConfigId, options);
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
        public Func<TPartySettings?, uint> teamSize { get; set; } = _ => 1;

        /// <summary>
        /// Number of teams in a game.
        /// </summary>
        public Func<TPartySettings?, uint> teamCount { get; set; } = _ => 2;

        /// <summary>
        /// Returns true if 2 party can play together.
        /// </summary>
        public Func<TPartySettings?, TPartySettings?, bool> canPlayTogether { get; set; } = (_, _) => true;

        /// <summary>
        /// party parameters factory method.
        /// </summary>
        public Func<Party, Task<TPartySettings?>> GetSettings { get; set; } = p =>
        {
            if (p.CustomData != null)
            {
                try
                {
                    return Task.FromResult<TPartySettings?>(JsonConvert.DeserializeObject<TPartySettings>(p.CustomData));
                }
                catch (Exception)
                {
                }
            }
            return Task.FromResult<TPartySettings?>(default);
        };





        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamSize(uint size)
        {

            return TeamSize(_ => size);
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="teams"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamCount(uint teams)
        {
            return TeamCount(_ => teams);
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="getTeamSize"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamSize(Func<TPartySettings?, uint> getTeamSize)
        {
            teamSize = getTeamSize;
            return this;
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="getTeams"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> TeamCount(Func<TPartySettings?, uint> getTeams)
        {
            teamCount = getTeams;
            return this;
        }

        /// <summary>
        /// Filters who can play together.
        /// </summary>
        /// <param name="canPlay"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> CanPlay(Func<TPartySettings?, TPartySettings?, bool> canPlay)
        {
            canPlayTogether = canPlay;
            return this;
        }

        /// <summary>
        /// Sets a function creating settings from a party.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public QuickQueueOptions<TPartySettings> SettingsGetter(Func<Party, Task<TPartySettings?>> value)
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
    }


    /// <summary>
    /// Options for the quick queue game finder implementation.
    /// </summary>
    public class QuickQueueOptions : QuickQueueOptionsBase
    {
        /// <summary>
        /// Size of the teams.
        /// </summary>
        public Func<dynamic?, uint> teamSize { get; set; } = _ => 1;

        /// <summary>
        /// Number of teams in a game.
        /// </summary>
        public Func<dynamic?, uint> teamCount { get; set; } = _ => 2;


        /// <summary>
        /// Can parties play together method.
        /// </summary>
        public Func<dynamic?, dynamic?, bool> CanPlayTogether { get; set; } = (_, _) => true;

        /// <summary>
        /// party parameters factory method.
        /// </summary>
        public Func<Party, Task<dynamic?>> getSettings { get; set; } = p =>
        {
            if (p.CustomData != null)
            {
                try
                {
                    return Task.FromResult<dynamic?>(JsonConvert.DeserializeObject<JObject>(p.CustomData));
                }
                catch (Exception)
                {
                }
            }
            return Task.FromResult<dynamic?>(null);
        };

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamSize(uint size)
        {

            return TeamSize(_ => size);
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="teams"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamCount(uint teams)
        {
            return TeamCount(_ => teams);
        }

        /// <summary>
        /// Sets the teamsize
        /// </summary>
        /// <param name="getTeamSize"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamSize(Func<dynamic?, uint> getTeamSize)
        {
            teamSize = getTeamSize;
            return this;
        }

        /// <summary>
        /// Sets the number of teams in a game.
        /// </summary>
        /// <param name="getTeams"></param>
        /// <returns></returns>
        public QuickQueueOptions TeamCount(Func<dynamic?, uint> getTeams)
        {
            teamCount = getTeams;
            return this;
        }

        /// <summary>
        /// Filters who can play together.
        /// </summary>
        /// <param name="canPlay"></param>
        /// <returns></returns>
        public QuickQueueOptions CanPlay(Func<dynamic?, dynamic?, bool> canPlay)
        {
            CanPlayTogether = canPlay;
            return this;
        }

        /// <summary>
        /// Sets a function creating settings from a party.
        /// </summary>
        /// <param name="value"></param>>
        /// <returns></returns>
        public QuickQueueOptions SettingsGetter(Func<Party, Task<dynamic?>> value)
        {
            getSettings = value;
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
