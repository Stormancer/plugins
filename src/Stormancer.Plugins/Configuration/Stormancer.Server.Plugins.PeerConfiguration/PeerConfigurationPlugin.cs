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
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PeerConfiguration
{
    class PeerConfigurationPlugin : IHostPlugin
    {
        public const string METADATA_KEY = "stormancer.peerConfig";
        public const string METADATA_GROUP_KEY = "stormancer.peerConfig.group";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneCreating += (ISceneHost scene) =>
            {
                if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                {
                    scene.TemplateMetadata["stormancer.peerConfig"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
                }
            };
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
              {
                  if (scene.Template == Users.Constants.SCENE_TEMPLATE)
                  {
                      builder.Register<PeerConfigurationService>().AsSelf().As<IConfigurationChangedEventHandler>().InstancePerScene();
                      builder.Register<UsersEventHandler>().As<IUserSessionEventHandler>().InstancePerRequest();
                  }
              };
        }
    }

    /// <summary>
    /// Entry class of Stormancer.Server.Plugins.PeerConfiguration.
    /// </summary>
    public class App
    {
        /// <summary>
        /// Configures the server application to add the plugin PeerConfiguration. 
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new PeerConfigurationPlugin());
        }
    }
}
