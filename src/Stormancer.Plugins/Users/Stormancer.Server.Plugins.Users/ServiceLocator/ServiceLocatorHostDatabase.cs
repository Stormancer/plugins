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
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    
    internal class ServiceLocatorHostDatabase
    {
        private ConcurrentDictionary<ServiceKey, ISceneHost> _scenes = new ConcurrentDictionary<ServiceKey, ISceneHost>();

        private struct ServiceKey
        {
            public ServiceKey(string type, string instanceId)
            {
                Type = type;
                InstanceId = instanceId;
            }
            public string Type { get; }
            public string InstanceId { get; }

            public override int GetHashCode() => Type.GetHashCode() * 17 + InstanceId.GetHashCode();

            public override bool Equals(object? obj)
            {
                if (obj == null)
                {
                    return false;
                }
                var other = (ServiceKey)obj;
                return Type == other.Type && InstanceId == other.InstanceId;
            }

        }
        internal void AddScene(string serviceType, string serviceInstanceId, ISceneHost scene)
        {
            if (string.IsNullOrEmpty(serviceType))
            {
                throw new ArgumentException($"« {nameof(serviceType)} » ne peut pas être vide ou avoir la valeur Null.", nameof(serviceType));
            }

            if (serviceInstanceId is null)
            {
                throw new ArgumentNullException(nameof(serviceInstanceId));
            }

            _scenes.AddOrUpdate(new ServiceKey(serviceType, serviceInstanceId), scene, (_, _) => scene);
        }

        internal void RemoveScene(ISceneHost scene)
        {

            //TODO: Optimization?
            foreach (var s in _scenes.ToArray())
            {
                if (s.Value == scene)
                {
                    _scenes.TryRemove(s.Key, out _);
                }
            }
        }

        public bool TryGetScene(string serviceType, string serviceInstanceId, [NotNullWhen(true)] out ISceneHost? scene) => _scenes.TryGetValue(new ServiceKey(serviceType, serviceInstanceId), out scene);

    }
}