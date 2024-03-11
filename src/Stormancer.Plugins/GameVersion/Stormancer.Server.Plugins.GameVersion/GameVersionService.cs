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

using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using System;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameVersion
{
    internal class GameVersionService: IConfigurationChangedEventHandler
    {
        public string CurrentGameVersion { get; private set; } = "NA";

        public bool CheckClientVersion { get; private set; } = false;

        private readonly string prefix;
        private readonly IConfiguration configuration;
        private readonly ISceneHost scene;

        public GameVersionService(IConfiguration configuration, IEnvironment environment, ISceneHost scene)
        {
            this.configuration = configuration;
            this.scene = scene;
            prefix = scene.TemplateMetadata[GameVersionPlugin.METADATA_KEY];

            environment.ActiveDeploymentChanged += (sender, v) =>
            {
                if (!environment.IsActive)
                {
                    scene.Broadcast("serverVersion.update", v.ActiveDeploymentId);
                }
            };

            
            UpdateSettings();

            scene.Connected.Add(p =>
            {
                p.Send("gameVersion.update", CurrentGameVersion);
                return Task.FromResult(true);
            });
        }

        private void UpdateSettings()
        {
            dynamic config = configuration.Settings;
            dynamic? configEntry;
            if (prefix == "default")
            {
                configEntry = config?.clientVersion;
            }
            else
            {
                configEntry = config?.clientVersion?[prefix];
            }

            var (newGameVersion, checkClientVersion) = GetVersion((JObject?)configEntry);
            if (newGameVersion != CurrentGameVersion)
            {
                CurrentGameVersion = newGameVersion;
                scene.Broadcast("gameVersion.update", CurrentGameVersion);
            }

            CheckClientVersion = checkClientVersion;
        }

        private (string, bool) GetVersion(JObject? c)
        {
            dynamic? configuration = c;
            string? gameVersion;
            bool checkClientVersion;
            try
            {
                gameVersion = configuration?.version;
            }
            catch (Exception)
            {
                gameVersion = null;
            }

            try
            {
                checkClientVersion = (bool?)configuration?.enableVersionChecking ?? false;
            }
            catch (Exception)
            {
                checkClientVersion = false;
            }

            if (gameVersion == null)
            {
                gameVersion = "NA";
            }
            return (gameVersion, checkClientVersion);
        }

        public void OnConfigurationChanged()
        {
            UpdateSettings();
        }
    }
}
