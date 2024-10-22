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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameVersion
{
    /// <summary>
    /// Game version configuration.
    /// </summary>
    public class GameVersionConfigurationSection : IConfigurationSection<GameVersionConfigurationSection>
    {
        /// <inheritdoc/>
        public static string SectionPath => "clientVersion";

        /// <inheritdoc/>
        public static GameVersionConfigurationSection Default { get; } = new GameVersionConfigurationSection();

        /// <summary>
        /// Enables version checking.
        /// </summary>
        /// <remarks>
        /// Defaults to false.
        /// </remarks>
        public bool enableVersionChecking { get; set; }

        /// <summary>
        /// Gets the authorized game version.
        /// </summary>
        /// <remarks>
        /// If <see cref="AuthorizedVersions"/> is set, it replaces this configuration field.
        /// </remarks>
        public string? version { get; set; }

        /// <summary>
        /// list of authorized game versions
        /// </summary>
        public IEnumerable<string> AuthorizedVersions { get; set; } = Enumerable.Empty<string>();


    }

    internal class GameVersionService : IConfigurationChangedEventHandler
    {
        public GameVersionConfigurationSection CurrentConfiguration { get; private set; } = new GameVersionConfigurationSection();

        private readonly IConfiguration configuration;
        private readonly ISceneHost scene;

        public GameVersionService(IConfiguration config, IEnvironment environment, ISceneHost scene)
        {
            this.configuration = config;
            this.scene = scene;


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
                p.Send("gameVersion.update", GetRecommendedVersionString());
                return Task.FromResult(true);
            });
        }
        private string GetRecommendedVersionString() => CurrentConfiguration.AuthorizedVersions.Any() ? string.Join(",", CurrentConfiguration.AuthorizedVersions) : CurrentConfiguration.version ?? "NA";
        private void UpdateSettings()
        {
            var config = configuration.GetValue<GameVersionConfigurationSection>(GameVersionConfigurationSection.SectionPath);

            var oldVersionString = GetRecommendedVersionString();
            CurrentConfiguration = config;
            var newVersionString = GetRecommendedVersionString();

            if (oldVersionString != newVersionString)
            {
                scene.Broadcast("gameVersion.update", newVersionString);
            }
        }

        public void OnConfigurationChanged()
        {
            UpdateSettings();
        }
    }
}
