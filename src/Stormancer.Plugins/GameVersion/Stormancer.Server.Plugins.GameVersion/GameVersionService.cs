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

using Stormancer.Server.Plugins.Configuration;
using Stormancer.Core;
using Stormancer.Server.Components;
using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameVersion
{
    internal class GameVersionService
    {
        public string CurrentGameVersion { get; private set; } = "NA";

        public bool CheckClientVersion { get; private set; } = false;

        private readonly string prefix;
        private readonly ISceneHost scene;

        public GameVersionService(IConfiguration configuration, IEnvironment environment, ISceneHost scene)
        {
            this.scene = scene;
            prefix = scene.Metadata[GameVersionPlugin.METADATA_KEY];

            environment.ActiveDeploymentChanged += (sender, v) =>
            {
                if (!environment.IsActive)
                {
                    scene.Broadcast("serverVersion.update", v.ActiveDeploymentId);
                }
            };

            configuration.SettingsChanged += (sender, v) => UpdateSettings(v);
            UpdateSettings(configuration.Settings);

            scene.Connected.Add(p =>
            {
                p.Send("gameVersion.update", CurrentGameVersion);
                return Task.FromResult(true);
            });
        }

        private void UpdateSettings(dynamic configuration)
        {
            dynamic configEntry;
            if (prefix == "default")
            {
                configEntry = configuration;
            }
            else
            {
                configEntry = configuration[prefix];
            }

            var newGameVersion = GetVersion(configEntry);
            if (newGameVersion != CurrentGameVersion)
            {
                CurrentGameVersion = newGameVersion;
                scene.Broadcast("gameVersion.update", CurrentGameVersion);
            }

            CheckClientVersion = (bool?)configEntry?.checkClientVersion ?? false;
        }

        private string GetVersion(dynamic configuration)
        {
            try
            {
                dynamic gameVersion = configuration.gameVersion;
                if (gameVersion != null)
                {
                    try
                    {
                        return (string)gameVersion;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception)
            {

            }
            return "NA";
        }
    }
}
