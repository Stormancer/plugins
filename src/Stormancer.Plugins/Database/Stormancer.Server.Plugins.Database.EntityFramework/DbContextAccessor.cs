using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stormancer.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore
{
    /// <summary>
    /// Provides access to an entity framework DBContext scoped to the current request.
    /// </summary>
    public class DbContextAccessor : IAsyncDisposable
    {
        private Dictionary<string, Task<AppDbContext>> _contexts = new Dictionary<string, Task<AppDbContext>>();
        private object _lock = new object();
        private readonly IEnumerable<IDbModelBuilder> _builders;
        private readonly IEnumerable<IDbContextLifecycleHandler> _lifecycleHandlers;
        private readonly ILogger _logger;

        public DbContextAccessor(IEnumerable<IDbModelBuilder> builders, IEnumerable<IDbContextLifecycleHandler> lifecycleHandlers, ILogger logger)
        {
            _builders = builders;
            _lifecycleHandlers = lifecycleHandlers;
            _logger = logger;
        }
        /// <summary>
        /// Gets the Entity framework database context.
        /// </summary>
        public Task<AppDbContext> GetDbContextAsync(string contextId = "default",CancellationToken cancellationToken = default)
        {
            async Task<AppDbContext> InitializeContext(CancellationToken cancellationToken)
            {
                try
                {
                    var handlerContext = new InitializeDbContext(contextId);
                    await _lifecycleHandlers.RunEventHandler(h => h.OnPreInit(handlerContext), ex => _logger.Log(LogLevel.Error, "database.entityframeworkcore.initialize", "An error occurred while executing OnPreInit", ex));
                    var ctx = new AppDbContext(contextId, handlerContext.CustomData, _builders, _lifecycleHandlers);
                    return ctx;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, "database.entityframeworkcore.initialize", $"An error occurred while initializing the DB context {contextId}", ex);
                    lock(_lock)
                    {
                        _contexts.Remove(contextId);
                    }
                    throw;
                }
            }
            lock (this._lock)
            {
                if (_contexts.TryGetValue(contextId, out var context))
                {
                    return context;
                }
                else
                {
                    context = InitializeContext(cancellationToken);
                    _contexts[contextId] = context;
                }
                return context;
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var context in _contexts.Values)
            {
                var disposable = await context;
                await disposable.DisposeAsync();
            }
        }
    }

    /// <summary>
    ///  Implementing <see cref="IDbModelBuilder"/> enables classes to participate in building the Database model.
    /// </summary>
    public interface IDbModelBuilder
    {
        void OnModelCreating(ModelBuilder modelBuilder, string contextId, Dictionary<string, object> customData);
    }

    /// <summary>
    /// Implementing <see cref="IDbContextLifecycleHandler"/> enables classes to configure the database context.
    /// </summary>
    public interface IDbContextLifecycleHandler
    {
        Task OnPreInit(InitializeDbContext ctx);
        void OnConfiguring(DbContextOptionsBuilder optionsBuilder,string contextId, Dictionary<string,object> customData);
    }

    /// <summary>
    /// Initialize DB context context
    /// </summary>
    public class InitializeDbContext
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="contextId"></param>
        public InitializeDbContext(string contextId)
        {
            ContextId = contextId;
        }
        /// <summary>
        /// Gets a dictionary containing custom context data which can be used to customize the initialization of the DB context.
        /// </summary>
        public Dictionary<string,object> CustomData { get; } = new Dictionary<string,object>();

        /// <summary>
        /// 
        /// </summary>
        public string ContextId { get; }
    }

    /// <summary>
    /// Database context.
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly IEnumerable<IDbModelBuilder> _builders;
        private readonly IEnumerable<IDbContextLifecycleHandler> _lifecycleHandlers;
        private Dictionary<string, object> _dbSets = new Dictionary<string, object>();

        /// <summary>
        /// Gets the context Id.
        /// </summary>
        public string Id { get; }
        private readonly Dictionary<string, object> _customData;
        internal AppDbContext(string id, Dictionary<string,object> customData, IEnumerable<IDbModelBuilder> builders, IEnumerable<IDbContextLifecycleHandler> lifecycleHandlers)
        {
            Id = id;
            _customData = customData;
            _builders = builders;
            _lifecycleHandlers = lifecycleHandlers;
        }

       
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            
            foreach (var handler in _lifecycleHandlers)
            {
                handler.OnConfiguring(optionsBuilder,Id,_customData);
            }

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var builder in _builders)
            {
                builder.OnModelCreating(modelBuilder,Id,_customData);
            }


            base.OnModelCreating(modelBuilder);
        }
    }
}
