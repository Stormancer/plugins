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
using Stormancer.Server.Plugins.Configuration;
using SmartFormat;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Stormancer.Core;
using System.Threading;
using Microsoft.IO;
using Stormancer.Server.Plugins.Utilities;
using Stormancer.Abstractions.Server.Components;

namespace Stormancer.Server.Plugins.ServiceLocator
{
    /// <summary>
    /// Configuration of the service locator.
    /// </summary>
    public class ServiceLocatorConfig
    {
        /// <summary>
        /// Default service locator mappings.
        /// </summary>
        /// <remarks>
        /// serviceType => sceneIdPattern
        /// The scene id pattern uses the SmartFormat format and is provided with serviceName and Context arguments.
        /// The default mapping is executed if no IServiceLocator implementation could locate the service.
        /// </remarks>
        public Dictionary<string, string> DefaultMapping { get; set; } = new Dictionary<string, string>();

    }

    internal class ServiceLocatorProviderRepository
    {
        public ServiceLocatorProviderRepository(IEnumerable<IServiceLocatorProvider> providers)
        {
            Providers = providers;
        }
        public IEnumerable<IServiceLocatorProvider> Providers { get; }
    }

    internal class ServiceLocator : IServiceLocator
    {
        private readonly IEnvironment _env;
        private readonly IClusterSerializer serializer;
        private readonly RecyclableMemoryStreamProvider _memoryStreamProvider;
        private readonly ISceneHost scene;
        private readonly IHost host;
        private readonly ServiceLocatorHostDatabase db;
        private readonly IScenesManager _management;
        private readonly ServiceLocatorProviderRepository _providers;
        private readonly ILogger _logger;

        private ServiceLocatorConfig? _config;

        public ServiceLocator(
           ServiceLocatorProviderRepository providers,
            IScenesManager management,
            IEnvironment env,
            IConfiguration config,
            IClusterSerializer serializer,
            Stormancer.Server.Plugins.Utilities.RecyclableMemoryStreamProvider memoryStreamProvider,
            ISceneHost scene,
            IHost host,
            ServiceLocatorHostDatabase db,
            ILogger logger)
        {
            _env = env;
            this.serializer = serializer;
            _memoryStreamProvider = memoryStreamProvider;
            this.scene = scene;
            this.host = host;
            this.db = db;
            _management = management;
            _providers = providers;
            _logger = logger;

            //config.SettingsChanged += (sender, args) => Config_SettingsChanged(args);
            Config_SettingsChanged(config.Settings);
        }

        private void Config_SettingsChanged(dynamic e)
        {
            _config = (e.serviceLocator as JObject)?.ToObject<ServiceLocatorConfig>() ?? new ServiceLocatorConfig();
        }

        public async Task<string> GetSceneConnectionToken(string serviceType, string serviceName, Session? session)
        {
            var sceneUri = await GetSceneId(serviceType, serviceName, session);

            if (sceneUri == null)
            {
                throw new ClientException("serviceNotFound");
            }
            try
            {

                using (var stream = _memoryStreamProvider.GetStream())
                {
                    if (session == null)
                    {
                        _logger.Log(LogLevel.Warn, "locator", "session is null", new { });
                    }
                    serializer.Serialize(stream,session);
                    var token = await _management.CreateConnectionTokenAsync(sceneUri, stream.ToArray(), "stormancer/userSession");

                    return token;
                }
            }
            catch (InvalidOperationException ex) when (ex.InnerException is HttpRequestException hre)
            {
                if (hre.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.Log(LogLevel.Warn, "serviceLocator", $"Scene {sceneUri} not found.", new { service = serviceType, name = serviceName });
                }

                throw;

            }
        }

        public async Task<string?> GetSceneId(string serviceType, string serviceName, Session? session)
        {
            var handlers = _providers.Providers;
            var ctx = new ServiceLocationCtx { ServiceName = serviceName, ServiceType = serviceType, Session = session };
            await handlers.RunEventHandler(slp => slp.LocateService(ctx), ex => _logger.Log(LogLevel.Error, "serviceLocator", "An error occurred while executing the LocateService extensibility point", ex));

            if (_config != null && string.IsNullOrEmpty(ctx.SceneId) && _config.DefaultMapping.TryGetValue(ctx.ServiceType, out var template))
            {
                ctx.SceneId = Smart.Format(template, ctx);
            }

            if(ctx.SceneId == null)
            {
                ctx.SceneId = await QueryClusterForSceneIdAsync(serviceType, serviceName, default);
            }
            return ctx.SceneId;
        }

        private async Task<string?> QueryClusterForSceneIdAsync(string serviceType, string serviceInstanceId,CancellationToken cancellationToken)
        {
            using var rq = await host.CreateAppFunctionRequest("ServiceLocator.Query", cancellationToken);
            serializer.Serialize(rq.Input,serviceType);
            serializer.Serialize(rq.Input,serviceInstanceId);


            rq.Input.Complete();
            rq.Send();
            await foreach(var response in rq.Results)
            {
                if(response.IsSuccess)
                {
                    var sceneId = await serializer.DeserializeAsync<string?>(response.Output, cancellationToken);
                    if(sceneId != null)
                    {
                        return sceneId;
                    }
                }
            }
            return null;
        }

        public async Task<IS2SRequest> CreateS2SRequestAsync(string serviceType, string serviceInstance, string route, CancellationToken cancellationToken)
        {
            var sceneId = await GetSceneId(serviceType, serviceInstance, null);
            if(sceneId == null)
            {
                throw new InvalidOperationException($"Failed to locate {serviceType}/{serviceInstance}.");
            }

            return await scene.CreateS2SRequestAsync(new MatchSceneFilter(sceneId), route, cancellationToken);
        }

        public async Task<IS2SRequest> StartS2SRequestAsync(string serviceType, string serviceInstance, string route, CancellationToken cancellationToken)
        {
           var rq = await CreateS2SRequestAsync(serviceType, serviceInstance, route, cancellationToken);

            rq.Send();
            return rq;
        }
    }
}