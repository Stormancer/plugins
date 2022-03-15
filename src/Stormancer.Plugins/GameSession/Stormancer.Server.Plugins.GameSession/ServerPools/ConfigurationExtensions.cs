using Newtonsoft.Json.Linq;
using Stormancer.Server;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.GameSession.ServerPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    /// <summary>
    /// Configuration object for server pools.
    /// </summary>
    public class ServerPoolsConfigurationBuilder
    {
        private readonly IHost host;

        internal ServerPoolsConfigurationBuilder(IHost host, JObject configSection)
        {
            this.host = host;
            Configuration = configSection;
        }


        /// <summary>
        /// Adds a composite server pool to the configuration.
        /// </summary>
        /// <remarks>
        /// Composite pools enables chaining pools, for instance so that dedicated servers are started locally in priority, then on an external systems as overflow.
        /// </remarks>
        /// <param name="poolId"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public ServerPoolsConfigurationBuilder CompositePool(string poolId, Func<CompositeServerPoolConfigurationBuilder, CompositeServerPoolConfigurationBuilder> builder)
        {

            JObject section;
            if (!Configuration.TryGetValue(poolId, out var sectionToken))
            {
                section = new JObject();
                Configuration[poolId] = section;
            }
            else
            {
                section = (JObject)sectionToken;
            }
            var configBuilder = new CompositeServerPoolConfigurationBuilder(poolId, section);

            builder(configBuilder);
            Configuration[poolId] = configBuilder.ConfigSection;


            return this;
        }

        /// <summary>
        /// Adds a pool made of local server processes.
        /// </summary>
        /// <param name="poolId"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public ServerPoolsConfigurationBuilder LocalProcessPool(string poolId, Func<LocalProcessPoolConfigurationBuilder, LocalProcessPoolConfigurationBuilder> builder)
        {
            JObject section;
            if (!Configuration.TryGetValue(poolId, out var sectionToken))
            {
                section = new JObject();
                Configuration[poolId] = section;
            }
            else
            {
                section = (JObject)sectionToken;
            }
            var configBuilder = new LocalProcessPoolConfigurationBuilder(poolId, section);

            builder(configBuilder);
            Configuration[poolId] = configBuilder.ConfigSection;

            return this;
        }

        /// <summary>
        /// Adds a pool made of docker containers running on a docker daemon.
        /// </summary>
        /// <param name="poolId"></param>
        /// <param name="configurator"></param>
        /// <returns></returns>
        public ServerPoolsConfigurationBuilder DockerPool(string poolId, Func<DockerPoolConfigurationBuilder, DockerPoolConfigurationBuilder> configurator)
        {
            JObject section;
            if (!Configuration.TryGetValue(poolId, out var sectionToken))
            {
                section = new JObject();
                Configuration[poolId] = section;
            }
            else
            {
                section = (JObject)sectionToken;
            }
            var configBuilder = new DockerPoolConfigurationBuilder(poolId, section);

            configurator(configBuilder);
            Configuration[poolId] = configBuilder.ConfigSection;
            return this;
        }

        /// <summary>
        /// Adds a pool of unauthenticated game servers that are started by the developers themselves without any authentication.
        /// </summary>
        /// <param name="poolId"></param>
        /// <remarks>
        /// Dev pools MUST NOT be enabled in production environments, as doing so would allow players to run unauthenticated self hosted game servers. 
        /// </remarks>
        /// <returns></returns>
        public ServerPoolsConfigurationBuilder DevPool(string poolId)
        {
            JObject section;
            if (!Configuration.TryGetValue(poolId, out var sectionToken))
            {
                section = JObject.FromObject(new DevPoolConfiguration());
                Configuration[poolId] = section;
            }
            host.ConfigureUsers(u =>
            {
                u.Settings[DevDedicatedServerAuthProvider.PROVIDER_NAME] = JObject.FromObject(new { enabled = true });
                return u;
            });


            return this;
        }

        /// <summary>
        /// Gets or sets the configuration section object as a json.
        /// </summary>
        public JObject Configuration { get; }
    }

    public class DevPoolConfiguration : PoolConfiguration
    {
        public override string type => "dev";
    }

    /// <summary>
    /// Base class for pool configurations.
    /// </summary>
    public abstract class PoolConfiguration
    {
        public abstract string type { get; }
        /// <summary>
        /// number of server ready to accept game sessions to maintain.
        /// </summary>
        public uint ready { get; set; }
    }

    /// <summary>
    /// Base class for Server pool configuration builders.
    /// </summary>
    public abstract class ServerPoolConfigurationBuilder
    {
        internal ServerPoolConfigurationBuilder(string poolId, JObject section)
        {
            ConfigSection = section;
            PoolId = poolId;
        }


        /// <summary>
        /// Gets the id of the server pool.
        /// </summary>
        public string PoolId { get; }


        /// <summary>
        /// Config section of the server pool.
        /// </summary>
        internal JObject ConfigSection { get; set; } = new JObject();

        /// <summary>
        /// Updates the configuration section of hte pool, as a statically typed object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="updater"></param>
        protected void UpdateConfiguration<T>(Action<T> updater) where T : new()
        {
            var config = ConfigSection.ToObject<T>() ?? new T();
            updater(config);
            ConfigSection = JObject.FromObject(config);
        }
    }

    /// <summary>
    /// Configuration of a composite pool.
    /// </summary>
    public class CompositeServerPoolConfigurationBuilder : ServerPoolConfigurationBuilder
    {
        internal CompositeServerPoolConfigurationBuilder(string poolId, JObject section) : base(poolId, section)
        {
        }

        /// <summary>
        /// Sets the number of servers the pool will maintain ready.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public CompositeServerPoolConfigurationBuilder ServersReadyInPool(uint value)
        {
            ConfigSection["ready"] = value;
            return this;
        }

        /// <summary>
        /// Adds a child pool to the composite pool.
        /// </summary>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public CompositeServerPoolConfigurationBuilder Child(string poolId)
        {

            JArray array;
            if (ConfigSection.TryGetValue("pools", out var token))
            {
                array = (JArray)token;
            }
            else
            {
                array = new JArray();
                ConfigSection["pools"] = array;
            }
            array.Add(poolId);
            return this;
        }
    }

    /// <summary>
    /// Configuration of a local process pool.
    /// </summary>
    public class LocalProcessPoolConfiguration : PoolConfiguration
    {


        /// <summary>
        /// Path to the package to use as a local server.
        /// </summary>
        /// <remarks></remarks>
        public string? path { get; set; }

        /// <summary>
        /// arguments passed to the container on startup.
        /// </summary>
        public IEnumerable<string>? arguments { get; set; }

        public override string type => "localProcess";
    }


    /// <summary>
    /// Configuration of a pool maintained in the local process space.
    /// </summary>
    public class LocalProcessPoolConfigurationBuilder : ServerPoolConfigurationBuilder
    {
        internal LocalProcessPoolConfigurationBuilder(string poolId, JObject section) : base(poolId, section)
        {
        }

        /// <summary>
        /// Sets the number of servers the pool will maintain ready.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public LocalProcessPoolConfigurationBuilder ServersReadyInPool(uint value)
        {
            UpdateConfiguration<LocalProcessPoolConfiguration>(c => c.ready = value);
            return this;
        }

        /// <summary>
        /// Sets the path to the dedicated server package that should be used.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public LocalProcessPoolConfigurationBuilder Path(string path)
        {
            UpdateConfiguration<LocalProcessPoolConfiguration>(c => c.path = path);
            return this;
        }

        /// <summary>
        /// Additional arguments passed to the server process.
        /// </summary>
        /// <remarks>
        /// Port leases and the auth token are passed as environment variables.
        /// </remarks>
        /// <param name="args"></param>
        /// <returns></returns>
        public LocalProcessPoolConfigurationBuilder Arguments(IEnumerable<string> args)
        {
            UpdateConfiguration<LocalProcessPoolConfiguration>(c => c.arguments = args);
            return this;
        }
    }

    /// <summary>
    /// Configuration of a docker pool.
    /// </summary>
    public class DockerPoolConfiguration : PoolConfiguration
    {


        /// <summary>
        /// Docker image to use.
        /// </summary>
        public string? image { get; set; }

        /// <summary>
        /// arguments passed to the container on startup.
        /// </summary>
        public IEnumerable<string>? arguments { get; set; }

        public override string type => "fromProvider";

        public string provider { get; set; } = "docker";

        /// <summary>
        /// Id of the de port pool used (delegated transport id)
        /// </summary>
        public string portPoolId { get; set; } = "public1";
    }

    /// <summary>
    /// Configuration of a pool maintained in a local docker daemon.
    /// </summary>
    public class DockerPoolConfigurationBuilder : ServerPoolConfigurationBuilder
    {
        internal DockerPoolConfigurationBuilder(string poolId, JObject section) : base(poolId, section)
        {

        }

        /// <summary>
        /// Sets the number of servers the pool will maintain ready.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public DockerPoolConfigurationBuilder ServersReadyInPool(uint value)
        {
            UpdateConfiguration<DockerPoolConfiguration>(c => c.ready = value);
            return this;
        }

        /// <summary>
        /// Sets the path to the docker image that contains the dedicated server.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public DockerPoolConfigurationBuilder Image(string path)
        {
            UpdateConfiguration<DockerPoolConfiguration>(c => c.image = path);
            return this;
        }


        /// <summary>
        /// Additional arguments passed to the server process.
        /// </summary>
        /// <remarks>
        /// Port leases and the auth token are passed as environment variables.
        /// </remarks>
        /// <param name="args"></param>
        /// <returns></returns>
        public DockerPoolConfigurationBuilder Arguments(IEnumerable<string> args)
        {
            UpdateConfiguration<DockerPoolConfiguration>(c => c.arguments = args);

            return this;
        }

        /// <summary>
        /// Sets the path to the docker image that contains the dedicated server.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DockerPoolConfigurationBuilder PortPoolId(string id)
        {
            UpdateConfiguration<DockerPoolConfiguration>(c => c.portPoolId = id);
            return this;
        }
    }

    /// <summary>
    /// Configuration object for a 
    /// </summary>
    public class DevPoolConfigurationBuilder : ServerPoolConfigurationBuilder
    {

        internal DevPoolConfigurationBuilder(string poolId, JObject section) : base(poolId, section)
        {
        }
    }




    /// <summary>
    /// Extension methods for server pool configuration.
    /// </summary>
    public static class ServerPoolsConfigurationExtensions
    {
        /// <summary>
        /// Configures the server pools in this application.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="configurator"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void ConfigureServerPools(this IHost host, Func<ServerPoolsConfigurationBuilder, ServerPoolsConfigurationBuilder> configurator)
        {
            if (configurator is null)
            {
                throw new ArgumentNullException(nameof(configurator));
            }
            var configuration = host.DependencyResolver.Resolve<IConfiguration>();
            var configSection = configuration.GetValue(ServerPoolsConstants.CONFIG_SECTION, new JObject());
            var builder = new ServerPoolsConfigurationBuilder(host,configSection);

            builder = configurator(builder);
            host.ConfigureUsers(u =>
            {
                u.Settings[DedicatedServerAuthProvider.PROVIDER_NAME] = JObject.FromObject(new { enabled = true });
               
                return u;
            });
            host.DependencyResolver.Resolve<IConfiguration>().SetDefaultValue(ServerPoolsConstants.CONFIG_SECTION, builder.Configuration);
        }
    }
}
