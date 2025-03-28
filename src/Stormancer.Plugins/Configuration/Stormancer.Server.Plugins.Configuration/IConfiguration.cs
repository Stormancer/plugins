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

using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Configuration
{
    /// <summary>
    /// Object registered in the dependency container implementing this interface are notified of configuration changes.
    /// </summary>
    /// <remarks>
    /// The object must be registered as an "host" or "scene" dependency. "Request" and "per dependency" scoped dependencies are not notified.
    /// </remarks>
    public interface IConfigurationChangedEventHandler
    {
        /// <summary>
        /// Method called when the configuration is updated.
        /// </summary>
        /// <remarks>
        /// Use <see cref="IConfiguration"/> to get the updated configuration.
        /// </remarks>
        void OnConfigurationChanged();

        /// <summary>
        /// Method called when the active deployment changes.
        /// </summary>
        /// <param name="e"></param>
        void OnDeploymentChanged(ActiveDeploymentChangedEventArgs e) { }
    }

    /// <summary>
    /// Application configuration.
    /// </summary>
    public interface IConfiguration
    {
        /// <summary>
        /// App config tree.
        /// </summary>
        dynamic Settings { get; }

        /// <summary>
        /// Gets a configuration object from the provided path.
        /// </summary>
        /// <typeparam name="T">Type of the object to deserialize.</typeparam>
        /// <param name="path"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        T? GetValue<T>(string path, T? defaultValue = default)
            => TryGetValue<T>(path, out var result) ? result : defaultValue;

        /// <summary>
        /// Gets a configuration object from the provided path if it is present
        /// </summary>
        /// <typeparam name="T">Type of the object to deserialize</typeparam>
        /// <param name="path"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool TryGetValue<T>(string path, out T? value);

        /// <summary>
        /// Gets a section of the configuration.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        IOptions<T> GetOptions<T>(string path) where T : class, new();

        /// <summary>
        /// Sets a default value for a configuration item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetDefaultValue<T>(string key, T value);
    }
    internal class DefaultConfiguration : IConfiguration
    {

        protected readonly IEnvironment _env;
        private readonly ConfigurationNotifier notifier;

        public DefaultConfiguration(IEnvironment environment, ConfigurationNotifier notifier)
        {
            _env = environment;
            this.notifier = notifier;
        }

        private static JObject defaultSettings = new JObject();

        public dynamic Settings
        {
            get
            {
                var settings = defaultSettings.DeepClone();
                ((JObject)settings).Merge(((JObject)_env.Configuration), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union, MergeNullValueHandling = MergeNullValueHandling.Merge, PropertyNameComparison = StringComparison.InvariantCulture });
                return settings;
            }
        }
        public void SetDefaultValue<T>(string path, T value)
        {
            var segments = path.Split('.');
            JObject node = defaultSettings;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                var next = node[segments[i]] as JObject;
                if (next == null)
                {
                    next = new JObject();
                    node[segments[i]] = next;
                }
                node = next;
            }
            node[segments.Last()] = JToken.FromObject(value!); //Ignore nullable error because FromObject supports null.


            notifier.NotifyConfigChanged();


        }


        public bool TryGetValue<T>(string path, out T? value)
        {
            var segments = path.Split('.');
            if (TryGetValue<T>(segments, Settings, out T? result))
            {
                value = result;
                return true;
            }
            else if (TryGetValue<T>(segments, defaultSettings, out result))
            {
                value = result;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        private bool TryGetValue<T>(string[] segments, JObject container, [MaybeNullWhen(false)] out T value)
        {

            var dic = new Dictionary<string, string>();

            JToken? node = container;
            for (int i = 0; i < segments.Length; i++)
            {
                node = node[segments[i]];
                if (node == null)
                {
                    value = default;
                    return false;
                }
            }

            value = node.ToObject<T>()!;
            return true;
        }

        public IOptions<T> GetOptions<T>(string path) where T : class, new()
        {
            return new Options<T>((path) => ((IConfiguration)this).GetValue<T>(path), path);
        }

        private class Options<T> : IOptions<T> where T : class, new()
        {
            private readonly Func<string, T?> _valueFactory;
            private readonly string _path;

            public Options(Func<string, T?> valueFactory, string path)
            {
                _valueFactory = valueFactory;
                _path = path;
            }
            public T Value => _valueFactory(_path) ?? new();
        }
    }


}


