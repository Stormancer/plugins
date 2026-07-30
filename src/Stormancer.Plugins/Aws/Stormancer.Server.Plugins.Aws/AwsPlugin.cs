using Stormancer.Diagnostics;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.Aws.Config;
using Stormancer.Server.Plugins.BlobStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Aws
{
    internal class AwsPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<ConfigCache>().SingleInstance();
                builder.Register<S3BlobStorageBackend>(resolver =>
                    new S3BlobStorageBackend(resolver.Resolve<ConfigCache>(), resolver.Resolve<ILogger>())
               ).As<IBlobStorageBackend>().InstancePerRequest();
            };
        }
    }
}
