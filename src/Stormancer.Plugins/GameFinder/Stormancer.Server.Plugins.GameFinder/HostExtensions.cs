using Stormancer.Server;
using Stormancer.Server.Plugins.GameFinder;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Stormancer
{
    /// <summary>
    /// Provides extension methods to configure gamefinders in an application.
    /// </summary>
    public static class HostExtensions
    {
        /// <summary>
        /// Adds a gamefinder to the application.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="id"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHost AddGameFinder(this IHost host, string id, Func<GameFinderConfig, GameFinderConfig> builder)
        {
            if(string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (!Regex.IsMatch(id, @"^[0-9a-z-_]*$", RegexOptions.IgnoreCase))
            {
                throw new ArgumentException("id should only contain alphanumeric, dash or underscore characters.");
            }

            List<string> scenes = new List<string>();

            if (host.Metadata.TryGetValue("gameFinder.ids", out var value))
            {
                scenes.AddRange(value.Split(','));
            }

            if(scenes.Contains(id))
            {
                throw new ArgumentException($"GameFinder '{id}' already exists.", "id");
            }

            host.AddSceneTemplate(id, scene =>
            {
                scene.AddGameFinder(id, b => builder(b));
            });

            scenes.Add(id);

            host.Metadata["gameFinder.ids"] = string.Join(',', scenes);

            return host;
        }
    }
}
