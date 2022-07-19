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

using Elasticsearch.Net;
using Nest;
using Nest.JsonNetSerializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database
{
    /// <summary>
    /// ES dabatase plugin entry point.
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="builder"></param>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ESClientPlugin());

        }
    }

    /// <summary>
    /// Basic ES Credentials.
    /// </summary>
    public class ESBasicCredentials
    {
        /// <summary>
        /// Login.
        /// </summary>
        public string? Login { get; set; }

        /// <summary>
        /// Password path in secrets store.
        /// </summary>
        public string? PasswordPath { get; set; }

        /// <summary>
        /// Password.
        /// </summary>
        public string? Password { get; set; }
    }

    /// <summary>
    /// ES Credentials.
    /// </summary>
    public class ESCredentials
    {
        /// <summary>
        /// Basic credentials.
        /// </summary>
        public ESBasicCredentials? Basic { get; set; }
    }

    /// <summary>
    /// Configuration of an ES connection pool.
    /// </summary>
    public class ESConnectionPoolConfig
    {
        /// <summary>
        /// Gets or sets the ES Endpoints
        /// </summary>
        // Replace : when the config specifies one or more endpoints, do not keep the default one.
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> Endpoints { get; set; } = new List<string> { "http://localhost:9200" };

        /// <summary>
        /// Is sniffing enabled on the ES connection.
        /// </summary>
        /// <remarks>
        /// Defaults to true.
        /// </remarks>
        public bool Sniffing { get; set; } = true;

        /// <summary>
        /// Should the client ignore certificate errors (for instance self signed cert).
        /// </summary>
        public bool IgnoreCertificateErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets ES credentials.
        /// </summary>
        public ESCredentials? Credentials { get; set; }
    }

    /// <summary>
    /// Represents a policy that maps an app storage id to an ES index.
    /// </summary>
    public class ESIndexPolicyConfig
    {

        /// <summary>
        /// Retry timeout.
        /// </summary>
        public int RetryTimeout { get; set; } = 5;

        /// <summary>
        /// Max number of retries before failure.
        /// </summary>
        public int MaxRetries { get; set; } = 5;

        /// <summary>
        /// Pattern used to transform the app index into an ES index.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Connection pool to use to interact with the index.
        /// </summary>
        public string? ConnectionPool { get; set; } = "default";

    }

    /// <summary>
    /// Elasticsearch config section.
    /// </summary>
    public class ESConfig
    {
        /// <summary>
        /// Default pattern used if no pattern is declated for an app index.
        /// </summary>
        public string defaultPattern = "{account}-{application}-{name}-{type}";

        /// <summary>
        /// Indice policies
        /// </summary>
        /// <remarks>
        /// If no policy exist for an app index, a default policy is generated. Create custom policies only to accomodate custom requirements.
        /// </remarks>
        public Dictionary<string, JObject> Indices { get; set; } = new Dictionary<string, JObject>();

        /// <summary>
        /// ES connection pools.
        /// </summary>
        public Dictionary<string, ESConnectionPoolConfig> ConnectionPools { get; set; } = new Dictionary<string, ESConnectionPoolConfig>();
    }

    internal class ESClientPlugin : IHostPlugin
    {
        private object synclock = new object();

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder b) =>
            {
                b.Register<ESClientFactory>().As<IESClientFactory>().As<IConfigurationChangedEventHandler>().SingleInstance();
                SmartFormat.Smart.Default.AddExtensions(new TimeIntervalFormatter());
            };

            ctx.HostStarted += (IHost host) =>
             {
                 host.DependencyResolver.Resolve<IESClientFactory>().Init();

             };
        }
    }

    public class ConnectionParameters
    {
        public string ConnectionPool { get; set; }
        public string IndexName { get; set; }
        public int maxRetries { get; set; }
        public int retryTimeout { get; set; }
    }

    /// <summary>
    ///Provide access to Elasticsearch DBs using centralized configuration policies
    /// </summary>
    /// <example>
    /// Configuration
    /// -------------
    /// 
    /// {
    ///     "elasticsearch":{
    ///         "indices":{
    ///             "gameData":{
    ///                 "retryTimeout":5,
    ///                 "maxRetries":5,
    ///                 "connectionPool":"game",
    ///                 
    ///             },
    ///             "leaderboards":{
    ///                 "retryTimeout":5,
    ///                 "maxRetries":5,
    ///                 "connectionPool":"game",
    ///                 "pattern":"leaderboards-{args.0}"
    ///             },
    ///             "analytics":{
    ///                 "retryTimeout":5,
    ///                 "maxRetries":1,
    ///                 "connectionPool":"analytics",
    ///                 //Pattern used to compute the actual index name. interval makes date interval based index names: 
    ///                 //interval(24) is daily, interval(168) weekly, interval(1) hourly, et...
    ///                 "pattern":"analytics-{type}-{args.0:interval(168)}"
    ///             }
    ///         },
    ///         "connectionPools":{
    ///             "game":{
    ///                 "sniffing":true, //Is the connection able to obtain updated endpoint info from the cluster (default to true)
    ///                 "endpoints":["http://localhost:9200"] //elasticsearch endpoints
    ///             },
    ///             "analytics":{
    ///                 "sniffing":true,
    ///                 "endpoints":["http://localhost:9200"]
    ///             }
    ///         }
    ///     }
    /// }
    /// 
    /// </example>
    public interface IESClientFactory
    {
        Task<Nest.IElasticClient> EnsureMappingCreated<T>(string name, Func<PutMappingDescriptor<T>, IPutMappingRequest> mappingDefinition, params object[] parameters) where T : class;
        Task<Nest.IElasticClient> CreateClient<T>(string name, params object[] parameters);
        Task<Nest.IElasticClient> CreateClient(string type, string name, params object[] parameters);
        ConnectionParameters GetConnectionParameters<T>(string name, params object[] parameters);
        string GetIndex<T>(string name, params object[] parameters);
        string GetIndex(string type, string name, params object[] parameters);
        IConnectionPool? GetConnectionPool(string id);
        Nest.IElasticClient CreateClient(ConnectionParameters p);
        Task Init();
    }

    public class IndexNameFormatContext
    {
        public string type;
        public string name;
        public string account;
        public string application;
        public object[] args;
        public string deployment;

        public Dictionary<string, object> ctx = new Dictionary<string, object>();
    }

    public interface IESClientFactoryEventHandler
    {
        void OnCreatingIndexName(IndexNameFormatContext ctx);
    }

    class ESClientFactory : IESClientFactory, IConfigurationChangedEventHandler, IDisposable
    {
        private const string LOG_CATEGORY = "ESClientFactory";

        private static ConcurrentDictionary<string, Task> _mappingInitialized = new ConcurrentDictionary<string, Task>();
        private IEnvironment _environment;
        private readonly IConfiguration _configuration;
        private ConcurrentDictionary<string, ElasticClient> _clients = new ConcurrentDictionary<string, ElasticClient>();
        private Dictionary<string, ConnectionPool> _connectionPools;
        private readonly ISecretsStore _secretsStore;

        private class ConnectionPool : IDisposable
        {
            public ConnectionPool(Elasticsearch.Net.IConnectionPool pool, ESCredentials? credentials, bool ignoreCertErrors)
            {
                Pool = pool;
                Credentials = credentials;
                IgnoreCertificateErrors = ignoreCertErrors;
            }

            public IConnectionPool Pool { get; }

            public ESCredentials? Credentials { get; }

            public bool IgnoreCertificateErrors { get; }
            public void Dispose()
            {
                Pool?.Dispose();
            }
        }

        private string _account;
        private string _application;
        private string _deploymentId;
        private ESConfig _config;
        private readonly ILogger _logger;
        private readonly Func<IEnumerable<IESClientFactoryEventHandler>> _eventHandlers;

        //private List<Elasticsearch.Net.Connection.HttpClientConnection> _connections = new List<Elasticsearch.Net.Connection.HttpClientConnection>();

        public ESClientFactory(IEnvironment environment, IConfiguration configuration, ILogger logger, Func<IEnumerable<IESClientFactoryEventHandler>> eventHandlers, ISecretsStore secretsStore)
        {
            _eventHandlers = eventHandlers;
            _environment = environment;
            _configuration = configuration;
            _logger = logger;
            _secretsStore = secretsStore;

            ApplySettings();
        }

        private void ApplySettings()
        {
            dynamic config = _configuration.Settings;
            _clients.Clear();
            _config = (ESConfig?)(config?.elasticsearch?.ToObject<ESConfig>()) ?? new ESConfig();

            _connectionPools = _config.ConnectionPools?.ToDictionary(kvp => kvp.Key, kvp =>
            {
                var c = kvp.Value;
                IConnectionPool pool;
                var endpoints = c.Endpoints.DefaultIfEmpty("http://localhost:9200").Select(endpoint => new Uri(endpoint));
                if (c.Sniffing)
                {
                    //var connectionEndpoints = ((JArray)config.esEndpoints).ToObject<string[]>();
                    pool = new SniffingConnectionPool(endpoints);
                }
                else
                {
                    pool = new StaticConnectionPool(endpoints);
                }
                return new ConnectionPool(pool, c.Credentials, c.IgnoreCertificateErrors);
            }) ?? new Dictionary<string, ConnectionPool>();

            if (!_connectionPools.ContainsKey("default"))
            {
                _connectionPools.Add("default", new ConnectionPool(new StaticConnectionPool(new[] { new Uri("http://localhost:9200") }), null, false));
            }
        }

        public Task<IElasticClient> CreateClient<T>(string name, object[] parameters)
        {
            return CreateClient(typeof(T).Name, name, parameters);
        }

        public Task<Nest.IElasticClient> CreateClient(string type, string name, params object[] parameters)
        {
            var p = GetConnectionParameters(type, name, parameters);
            return Task.FromResult(CreateClient(p));
        }

        public void Dispose()
        {
            foreach (var pool in _connectionPools.Values)
            {
                pool.Dispose();
            }
        }

        public ConnectionParameters GetConnectionParameters<T>(string name, params object[] parameters)
        {
            return GetConnectionParameters(typeof(T).Name.ToLowerInvariant(), name, parameters);
        }

        public ConnectionParameters GetConnectionParameters(string type, string name, params object[] parameters)
        {
            string? indexName = null;

            ESIndexPolicyConfig? policyConfig;
            if (_config.Indices.TryGetValue(name, out var indexConfig))
            {
                policyConfig = indexConfig.ToObject<ESIndexPolicyConfig>();
            }
            else
            {
                policyConfig = new ESIndexPolicyConfig();
            }

            var formatCtx = new IndexNameFormatContext
            {
                account = _account,
                application = _application,
                deployment = _deploymentId,
                args = parameters,
                type = type,
                name = name
            };

            _eventHandlers().RunEventHandler<IESClientFactoryEventHandler>(e => e.OnCreatingIndexName(formatCtx), ex =>
            {
                _logger.Log(Stormancer.Diagnostics.LogLevel.Error, LOG_CATEGORY, "An error occured while running an 'database.OnCreate' event handler", ex);
            });

            var pattern = policyConfig?.Pattern;
            if (pattern == null)
            {
                pattern = _config.defaultPattern;
            }

            indexName = SmartFormat.Smart.Format(pattern, formatCtx);

            if (string.IsNullOrWhiteSpace(indexName))
            {
                indexName = $"{name}-{type}";
            }

            return new ConnectionParameters { ConnectionPool = policyConfig?.ConnectionPool ?? "default", IndexName = indexName.ToLowerInvariant(), maxRetries = policyConfig.MaxRetries, retryTimeout = policyConfig.RetryTimeout };
        }

        public IElasticClient CreateClient(ConnectionParameters p)
        {

            if (!_connectionPools.TryGetValue(p.ConnectionPool, out var connectionPool))
            {
                _logger.Log(Stormancer.Diagnostics.LogLevel.Trace, "es", "Failed to find connection Pool", new { });
                throw new InvalidOperationException($"Failed to find connection pool {p.ConnectionPool} in elasticsearch config.");
            }

            return _clients.GetOrAdd(p.IndexName, i =>
            {
                var t = typeof(JToken);

                ConnectionSettings.SourceSerializerFactory s = (IElasticsearchSerializer s, IConnectionSettingsValues v) => new JsonNetSerializer(s, v);

                var settings = new ConnectionSettings(connectionPool.Pool, s).DefaultIndex(p.IndexName.ToLowerInvariant()).MaximumRetries(p.maxRetries).MaxRetryTimeout(TimeSpan.FromSeconds(p.retryTimeout));
                if (connectionPool?.Credentials?.Basic?.Login != null && connectionPool?.Credentials?.Basic?.Password != null)
                {

                    settings = settings.BasicAuthentication(connectionPool?.Credentials?.Basic?.Login, connectionPool?.Credentials?.Basic?.Password);
                }

                if(connectionPool?.IgnoreCertificateErrors?? false)
                {
                    settings = settings.ServerCertificateValidationCallback((_, cert, chain, policyErrors) =>
                    {

                        return true;
                    });
                }
                return new Nest.ElasticClient(settings);
            });
        }

        public string GetIndex<T>(string name, params object[] parameters)
        {
            return GetIndex(typeof(T).Name, name, parameters);
        }

        public string GetIndex(string type, string name, params object[] parameters)
        {
            return GetConnectionParameters(type, name, parameters).IndexName;
        }

        public IConnectionPool? GetConnectionPool(string id)
        {

            if (_connectionPools.TryGetValue(id, out var connection))
            {
                return connection.Pool;
            }
            else
            {
                if (_connectionPools.TryGetValue("default", out connection))
                {
                    return connection.Pool;
                }
                else
                {
                    return null;
                }
            }
        }

        public async Task<IElasticClient> EnsureMappingCreated<T>(string name, Func<PutMappingDescriptor<T>, IPutMappingRequest> mappingDefinition, params object[] parameters) where T : class
        {
            var client = await CreateClient<T>(name, parameters);

            await _mappingInitialized.GetOrAdd(client.ConnectionSettings.DefaultIndex, index => CreateMapping<T>(client, mappingDefinition));

            return client;
        }

        private async Task CreateMapping<T>(IElasticClient client, Func<PutMappingDescriptor<T>, IPutMappingRequest> mapping) where T : class
        {
            if (!(await client.Indices.ExistsAsync(client.ConnectionSettings.DefaultIndex)).Exists)
            {
                await client.Indices.CreateAsync(client.ConnectionSettings.DefaultIndex);
                await client.MapAsync<T>(mapping);
            }
        }

        public async Task Init()
        {
            var app = await _environment.GetApplicationInfos();

            _account = app.AccountId;
            _application = app.ApplicationName;
            _deploymentId = app.DeploymentId;

            foreach (var pair in _connectionPools)
            {
                var cp = pair.Value;
                if (cp.Credentials?.Basic != null)
                {
                    if (!string.IsNullOrWhiteSpace(cp.Credentials.Basic.PasswordPath))
                    {
                        var passwordSecret = await _secretsStore.GetSecret(cp.Credentials.Basic.PasswordPath);
                        if (passwordSecret != null && passwordSecret.Value != null)
                        {
                            cp.Credentials.Basic.Password = Encoding.UTF8.GetString(passwordSecret.Value);
                        }
                    }
                }
            }
        }

        public void OnConfigurationChanged()
        {
            ApplySettings();
            _ = Init();
        }
    }
}
