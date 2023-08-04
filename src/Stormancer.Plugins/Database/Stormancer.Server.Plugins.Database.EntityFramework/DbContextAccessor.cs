using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore
{
    /// <summary>
    /// Provides access to an entity framework DBContext scoped to the current request.
    /// </summary>
    public class DbContextAccessor :IAsyncDisposable
    {
        public DbContextAccessor(IEnumerable<IDbModelBuilder> builders, IEnumerable<IDbContextLifecycleHandler> lifecycleHandlers)
        {
            DbContext = new AppDbContext(builders, lifecycleHandlers);
        }
        /// <summary>
        /// Gets the Entity framework database context.
        /// </summary>
        public AppDbContext DbContext { get; }

        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }

    /// <summary>
    ///  Implementing <see cref="IDbModelBuilder"/> enables classes to participate in building the Database model.
    /// </summary>
    public interface IDbModelBuilder
    {
        void OnModelCreating(ModelBuilder modelBuilder);
    }

    /// <summary>
    /// Implementing <see cref="IDbContextLifecycleHandler"/> enables classes to configure the database context.
    /// </summary>
    public interface IDbContextLifecycleHandler
    {
        void OnConfiguring(DbContextOptionsBuilder optionsBuilder);
    }

    /// <summary>
    /// Database context.
    /// </summary>
    public class AppDbContext: DbContext
    {
        private readonly IEnumerable<IDbModelBuilder> _builders;
        private readonly IEnumerable<IDbContextLifecycleHandler> _lifecycleHandlers;
        private Dictionary<string, object> _dbSets = new Dictionary<string, object>();

        
        internal AppDbContext(IEnumerable<IDbModelBuilder> builders, IEnumerable<IDbContextLifecycleHandler> lifecycleHandlers)
        {
            _builders = builders;
            _lifecycleHandlers = lifecycleHandlers;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            foreach(var handler in _lifecycleHandlers)
            {
                handler.OnConfiguring(optionsBuilder);
            }
            
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var builder in _builders)
            {
                builder.OnModelCreating(modelBuilder);
            }

            this.Set()
            base.OnModelCreating(modelBuilder);
        }
    }
}
