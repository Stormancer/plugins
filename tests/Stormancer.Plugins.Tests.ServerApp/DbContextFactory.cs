using Microsoft.EntityFrameworkCore.Design;
using Stormancer.Server.Hosting;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.Tests.ServerApp
{
    public class DbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var host = ServerApplication.CreateDesignTimeHost(builder => builder.AddAllStartupActions());
            var scope = host.DependencyResolver.CreateChild(Stormancer.Server.Plugins.API.Constants.ApiRequestTag);

            return scope.Resolve<DbContextAccessor>().GetDbContextAsync().Result;
        }
    }
}
