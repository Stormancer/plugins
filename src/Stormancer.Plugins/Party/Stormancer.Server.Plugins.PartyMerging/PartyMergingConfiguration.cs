using Microsoft.AspNetCore.Server.IIS.Core;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PartyMerging
{
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
    }

    internal class PartyMergingConfigurationRepository
    {
        public void AddConfiguration(string id, Action<PartyMergingConfiguration> configurator)
        {

            _sceneConfigurators.Add((ISceneHost scene) =>
            {


            });
            _dependencyBuilderConfigurators.Add((ISceneHost scene, IDependencyBuilder builder) =>
            {
                if (scene.Metadata.TryGetValue("stormancer.partyMerger", out var partyMergerId) && partyMergerId == id)
                {
                    var config = new PartyMergingConfiguration(scene, builder);
                    configurator(config);
                }
            });
        }

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
}
