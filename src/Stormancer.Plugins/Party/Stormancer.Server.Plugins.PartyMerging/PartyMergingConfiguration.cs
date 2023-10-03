using Microsoft.AspNetCore.Server.IIS.Core;
using Org.BouncyCastle.Security;
using Stormancer.Core;
using Stormancer.Server.Plugins.Party;
using Stormancer.Server.Plugins.PartyFinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
    /// <summary>
    /// Configuration of a party merger.
    /// </summary>
    public class PartyMergingConfiguration
    {
        internal PartyMergingConfiguration(ISceneHost scene, IDependencyBuilder dependencyBuilder)
        {
            Scene = scene;
            DependencyBuilder = dependencyBuilder;
        }

        /// <summary>
        /// Party merger scene.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Gets the dependency builder enabling custom types to be added to the scene dependencies.
        /// </summary>
        public IDependencyBuilder DependencyBuilder { get; }

        /// <summary>
        /// Sets the Algorithm class of the party merger.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PartyMergingConfiguration Algorithm<T>() where T : class, IPartyMergingAlgorithm
        {
            DependencyBuilder.Register<T>().As<IPartyMergingAlgorithm>();
            return this;
        }

        /// <summary>
        /// Sets the Algorithm class of the party merger and provides a factory.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PartyMergingConfiguration Algorithm<T>(Func<IDependencyResolver,T> factory) where T : class, IPartyMergingAlgorithm
        {
            DependencyBuilder.Register<T>(factory).As<IPartyMergingAlgorithm>();
            return this;
        }
    }

    internal class PartyMergingConfigurationRepository
    {
        public void AddPartyMerger(string id, Action<PartyMergingConfiguration> configurator)
        {

            _sceneConfigurators.Add((ISceneHost scene) =>
            {


            });
            _dependencyBuilderConfigurators.Add((ISceneHost scene, IDependencyBuilder builder) =>
            {
                if (PartyMergingConstants.TryGetMergerId(scene,out var mergerId) && mergerId == id)
                {
                    var config = new PartyMergingConfiguration(scene, builder);
                    configurator(config);
                }
            });
            Scenes.Add(PartyMergingConstants.PARTYMERGER_PREFIX + id);
            
        }
        internal List<string> Scenes = new List<string>();
       

        private List<Action<ISceneHost>> _sceneConfigurators = new List<Action<ISceneHost>>();
        private List<Action<ISceneHost, IDependencyBuilder>> _dependencyBuilderConfigurators = new List<Action<ISceneHost, IDependencyBuilder>>();

        internal void ConfigureDependencyResolver(ISceneHost scene, IDependencyBuilder builder)
        {
            foreach (var action in _dependencyBuilderConfigurators)
            {
                action(scene, builder);
            }
        }

    }

    /// <summary>
    /// Class containing extension methods used to configure party mergers.
    /// </summary>
    public static class PartyMergingConfigurationExtensions
    {
        /// <summary>
        /// Configures a party merger.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="id">if of the party merger</param>
        /// <param name="configurator"></param>
        /// <returns></returns>
        public static IHost ConfigurePartyMerger(this IHost host, string id, Action<PartyMergingConfiguration> configurator)
        {
            host.DependencyResolver.Resolve<PartyMergingConfigurationRepository>().AddPartyMerger(id, configurator);
            return host;
        }
    }
}
