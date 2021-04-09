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
using Stormancer.Plugins;
using Stormancer.Server.Plugins.ServiceLocator;

namespace Stormancer.Server.Plugins.Profile
{
    class ProfilePlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.profiles";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ProfileController>().InstancePerRequest();
                builder.Register<ProfileService>().As<IProfileService>().InstancePerRequest();
                builder.Register<PseudoProfilePart>().As<IProfilePartBuilder>();
                builder.Register<CustomPartBuilder>().As<IProfilePartBuilder>();
                builder.Register<AttributeBasedCustomPart>().As<ICustomProfilePart>().SingleInstance();
                builder.Register<ProfileServiceLocator>().As<IServiceLocatorProvider>();
            };

            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<ProfileController>();
                }
            };

            ctx.HostStarting += (IHost host) =>
            {
                host.AddSceneTemplate(ProfilePluginConstants.SCENE_TEMPLATE, s => s.AddProfiles());
            };

            ctx.HostStarted += (IHost host) =>
            {
                host.EnsureSceneExists(ProfilePluginConstants.SCENE_ID, ProfilePluginConstants.SCENE_TEMPLATE, false, true);
            };

        }

    }

    internal class ProfileServiceLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            if (ctx.ServiceType == "stormancer.profiles")
            {
                ctx.SceneId = "profiles";
            }
            return Task.CompletedTask;
        }
    }
}

namespace Stormancer
{
    /// <summary>
    /// Extension methods for scenes.
    /// </summary>
    public static class ProfileExtensions
    {
        /// <summary>
        /// Adds the profiles API to the scene.
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static ISceneHost AddProfiles(this ISceneHost scene)
        {
            scene.Metadata[Stormancer.Server.Plugins.Profile.ProfilePlugin.METADATA_KEY] = "enabled";
            return scene;
        }
    }
}
