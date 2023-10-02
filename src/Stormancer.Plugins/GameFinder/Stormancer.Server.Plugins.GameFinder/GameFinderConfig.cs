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
using System;

namespace Stormancer.Server.Plugins.GameFinder
{
    /// <summary>
    /// Config for game finders.
    /// </summary>
    public class GameFinderConfig
    {
        internal GameFinderConfig(ISceneHost scene, string configId)
        {
            this.Scene = scene;
            ConfigId = configId;
        }
        private Action<IDependencyBuilder>? _registerDependencies;
        private Action<ISceneHost>? _onCreatingSceneCallbacks;
        /// <summary>
        /// Gets the id of the game finder being configured.
        /// </summary>
        public string GameFinderId => Scene.Id;

        /// <summary>
        /// Gets the scene running the game finder being configured.
        /// </summary>
        public ISceneHost Scene { get; }

        /// <summary>
        /// Id of the section storing the gamefinder config in the application configuration.
        /// </summary>
        public string ConfigId { get; }

       
        /// <summary>
        /// Configures dependencies for the scene.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public GameFinderConfig ConfigureDependencies(Action<IDependencyBuilder> builder)
        {
            _registerDependencies += builder;
            return this;
        }

        internal void RegisterDependencies(IDependencyBuilder builder)
        {
            _registerDependencies?.Invoke(builder);

        }

        /// <summary>
        /// Configures an action being run when the scene creating event is fired on the gamefinder scene.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public GameFinderConfig ConfigureOnCreatingScene(Action<ISceneHost> builder)
        {
            _onCreatingSceneCallbacks += builder;
            return this;
        }

        internal void RunOnCreatingScene(ISceneHost scene)
        {
            _onCreatingSceneCallbacks?.Invoke(scene);
        }
    }
}
