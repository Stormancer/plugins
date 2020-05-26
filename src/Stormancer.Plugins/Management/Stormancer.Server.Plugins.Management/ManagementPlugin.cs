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
using Stormancer.Diagnostics;
using Stormancer.Management;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Management
{
    /// <summary>
    /// Plugin startup class
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Run method for the plugin.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ManagementPlugin());
        }
    }
    class ManagementPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder b) =>
            {
                b.Register<ManagementClientProvider>().SingleInstance();
            };
        }
    }

    /// <summary>
    /// Provides a preconfigured management client for the federation.
    /// </summary>
    public class ManagementClientProvider
    {
        private readonly IEnvironment _environment;
        private readonly ILogger _logger;

        private ManagementClient _clientV3;
        private Lazy<ManagementClient> _clientV1;
        private ManagementClient clientV1 { get => _clientV1.Value; }

        public ManagementClientProvider(IEnvironment environment, ILogger logger)
        {
            _environment = environment;
            _logger = logger;

            async Task<Uri> EndpointResolver(string? clusterId)
            {
                var fed = await _environment.GetFederation();
                if (string.IsNullOrEmpty(clusterId))
                {
                    var endpoint = fed.current.adminEndpoints.FirstOrDefault();
                    if (endpoint == null)
                    {
                        throw new InvalidOperationException($"No admin endpoint found on {fed.current.id}");
                    }
                    return new Uri(endpoint);
                }
                else
                {
                    var cluster = fed.clusters.FirstOrDefault(c => c.id == clusterId);
                    if (cluster == null)
                    {
                        throw new InvalidOperationException($"Cluster {clusterId} not found in federation (Clusters are {string.Join(',', fed.clusters.Select(c => c.id))})");
                    }

                    if (!cluster.adminEndpoints.Any())
                    {
                        throw new InvalidOperationException($"No admin endpoint found on {cluster.id}");
                    }
                    return new Uri(cluster.adminEndpoints.First());
                }
            }

            _clientV3 = new ManagementClient(EndpointResolver, environment.GetBearerToken);

            _clientV1 = new Lazy<ManagementClient>(() => new ManagementClient(EndpointResolver, environment.GetBearerToken, "1"));
        }

        /// <summary>
        /// Creates a connection token for a scene in the federation.
        /// </summary>
        /// <param name="sceneUri"></param>
        /// <param name="payload"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public async Task<string> CreateConnectionToken(string sceneUri, byte[]? payload = null, string contentType = "application/octet-stream")
        {

            (var clusterId, var accountId, var applicationId, var sceneId) = await DecomposeSceneId(sceneUri);
            return await _clientV3.Applications.CreateConnectionToken(clusterId, accountId, applicationId, sceneId, payload ?? new byte[0], contentType);

        }

        /// <summary>
        /// Creates a connection token for a scene in the federation, using the V1 protocol.
        /// </summary>
        /// <param name="sceneUri"></param>
        /// <param name="payload"></param>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public async Task<string> CreateConnectionTokenV1(string sceneUri, byte[]? payload = null, string contentType = "application/octet-stream")
        {

            (var clusterId, var accountId, var applicationId, var sceneId) = await DecomposeSceneId(sceneUri);
            return await clientV1.Applications.CreateConnectionToken(clusterId, accountId, applicationId, sceneId, payload ?? new byte[0], contentType);

        }

        /// <summary>
        /// Creates a scene in the federation using the provider scene uri.
        /// </summary>
        /// <param name="sceneUri"></param>
        /// <param name="template"></param>
        /// <param name="isPublic"></param>
        /// <param name="isPersistent"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public async Task CreateScene(string sceneUri, string template, bool isPublic, bool isPersistent, JObject? metadata = null)
        {

            (var clusterId, var accountId, var applicationId, var sceneId) = await DecomposeSceneId(sceneUri);
            try
            {
                await _clientV3.Applications.CreateScene(clusterId, accountId, applicationId, sceneId, template, isPublic, metadata, isPersistent);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "manage", $"Failed to create the scene {sceneUri} on {clusterId}", ex);
                throw;
            }
        }

        private async Task<(string, string, string, string)> DecomposeSceneId(string sceneUri)
        {

            (var clusterId, var account, var app, var sceneId) = ParseSceneUri(sceneUri);
            var federation = await _environment.GetFederation();
            var appInfos = await _environment.GetApplicationInfos();

            if (clusterId == null)
            {
                clusterId = federation.current.id;
            }
            if (app == null)
            {
                app = appInfos.ApplicationName;
            }
            if (account == null)
            {
                account = appInfos.AccountId;
            }
            return (clusterId, account, app, sceneId);

        }

        
        private (string?, string?, string?, string) ParseSceneUri(string uri)
        {
            string? clusterId = null, account = null, application = null;
            string sceneId;
            if (uri.ToLowerInvariant().StartsWith("scene:"))
            {
                var segments = uri.Split('/');
                sceneId = segments[segments.Length - 1];
                if (segments.Length == 7)
                {
                    // scene:/cluster/account/application/deployment/shard/sceneId
                    clusterId = segments[1];
                    account = segments[2];
                    application = segments[3];
                }
                else
                {
                    //scene:/app/scene
                    if (segments.Length > 2)
                    {
                        application = segments[segments.Length - 2];

                    }
                    //scene:/account/app/scene
                    if (segments.Length > 3)
                    {
                        account = segments[segments.Length - 3];
                    }

                    //scene:/clusterId/account/app/scene
                    if (segments.Length > 4)
                    {
                        clusterId = segments[segments.Length - 4];
                    }
                }
            }
            else
            {
                sceneId = uri;
            }


            return (clusterId, account, application, sceneId);
        }

        /// <summary>
        /// Gets a management client instance for the current application.
        /// </summary>
        /// <returns></returns>
        public ManagementClient GetManagementClient()
        {
            return _clientV3;
        }
    }
}
