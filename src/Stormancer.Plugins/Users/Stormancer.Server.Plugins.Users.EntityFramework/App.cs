using Stormancer.Plugins;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.EntityFramework
{
    /// <summary>
    /// Entry point class
    /// </summary>
    public class App
    {
        // <summary>
        /// Plugin startup function (internal use only)
        /// </summary>
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new EntityFrameworkUserStoragePlugin());
        }
    }

    internal class EntityFrameworkUserStoragePlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += (IDependencyBuilder builder) =>
            {
                builder.Register<UserDbModelBuilder>().As<IDbModelBuilder>();
                builder.Register<EntityFrameworkUserStorage>().As<IUserStorage>().InstancePerRequest();
            };
        }
    }
}
