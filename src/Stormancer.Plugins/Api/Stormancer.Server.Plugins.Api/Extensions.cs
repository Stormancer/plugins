using Stormancer.Server.Plugins.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stormancer.Core
{
    /// <summary>
    /// Provides extension methods to scenes.
    /// </summary>
    public static class SceneExtensions
    {
        /// <summary>
        /// Creates a request scope from a scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static IDependencyResolver CreateRequestScope(this ISceneHost scene)
        {
            return scene.DependencyResolver.CreateChild(Constants.ApiRequestTag);
        }
    }
}
