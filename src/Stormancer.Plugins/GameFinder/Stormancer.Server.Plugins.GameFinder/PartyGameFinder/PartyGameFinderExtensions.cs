using Stormancer.Server.Plugins.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Provides Extension methods to configure game finders that create game sessions from a party.
    /// </summary>
    public static class PartyGameFinderExtensions
    {
        /// <summary>
        /// Configures a gamefinder that creates a game session for each party entering it.
        /// </summary>
        /// <param name="gameFinderConfig"></param>
        /// <param name="optionsBuilder"></param>
        /// <returns></returns>
        public static GameFinderConfig ConfigurePartyGameFinder(this GameFinderConfig gameFinderConfig, Func<PartyGameFinderOptions, PartyGameFinderOptions> optionsBuilder)
        {
            gameFinderConfig.ConfigureDependencies(dep =>
            {
                dep.Register<PartyGameFinder>().As<IGameFinderAlgorithm>().InstancePerRequest();
                dep.Register<PartyGameFinderResolver>().As<IGameFinderResolver>().InstancePerRequest();
            });


            var options = optionsBuilder(new PartyGameFinderOptions());

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
       

    }

    /// <summary>
    /// Base class for Quickqueue option objects, contains common options.
    /// </summary>
    public class PartyGameFinderOptions
    {
        /// <summary>
        /// Id of the template the game finder should use to create game session scenes.
        /// </summary>
        public string gameSessionTemplate { get; set; } = "gameSession";

        /// <summary>
        /// Is the party leader the host of gamesession?
        /// </summary>
        public bool partyLeaderIsHost { get; set; } = false;

        /// <summary>
        /// Action executed when creating a game.
        /// </summary>
        public Action<IDependencyResolver,PartyGameSessionConfig, NewGame> onCreatingGame { get; set; } = (_,_,_) => { };

        /// <summary>
        /// If true, the host of a P2P game session must be the aprty leader. If not, the game session is free to choose 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PartyGameFinderOptions PartyLeaderIsHost(bool value)
        {
            partyLeaderIsHost = value;
            return this;
        }

        /// <summary>
        /// Set a method called to customize the newly created game.
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public PartyGameFinderOptions OnCreatingGame(Action<IDependencyResolver,PartyGameSessionConfig,NewGame> func)
        {
            onCreatingGame = func;
            return this;
        }

        /// <summary>
        /// Sets the template to use to create gamesessions.
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        public PartyGameFinderOptions GameSessionTemplate(string template)
        {
            gameSessionTemplate = template;
            return this;
        }
    }
}
