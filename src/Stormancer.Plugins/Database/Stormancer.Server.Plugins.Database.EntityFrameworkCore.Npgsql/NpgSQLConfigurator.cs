using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore.Npgsql
{
    /// <summary>
    /// Configuration section for the PostgreSQL database connection. 
    /// </summary>
    public class NpgSQLConfigurationSection : IConfigurationSection<NpgSQLConfigurationSection>
    {
        ///<inheritdoc/>
        public static string SectionPath { get; } = "npgsql";

        ///<inheritdoc/>
        public static NpgSQLConfigurationSection Default { get; } = new NpgSQLConfigurationSection();
       

        /// <summary>
        /// Gets or sets the host of the postgreSQL server.
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// Gets or sets the username to use when authenticating.
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Gets or sets the path of the password in the secret stores.
        /// 
        /// </summary>
        /// <remarks>
        /// Format : {accountId}/{secretStoreId}/{secretId}
        /// </remarks>
        public string? PasswordPath { get; set; }

        internal string? Password { get; set; }

        /// <summary>
        /// Gets or sets the name of the database to use.
        /// </summary>
        public string? Database { get; set; }

        [MemberNotNullWhen(true, nameof(Username))]
        [MemberNotNullWhen(true, nameof(Password))]
        [MemberNotNullWhen(true, nameof(Database))]
        [MemberNotNullWhen(true, nameof(Host))]
        internal bool IsValid => !string.IsNullOrEmpty(Host) && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(Database);

        /// <summary>
        /// Gets or sets the max connection pool size for postgresql
        /// </summary>
        /// <remarks>
        /// Defaults to 50. Beware that by default Postgresql is configured with a max connection count at 100. As each node might create connections to the database, more than 2 nodes might hit the limit. In this case, it might be necessary to use something like pgpool to better control connections to the server
        /// </remarks>
        public int MaxPoolSize { get; set; } = 50;

       
    }
    internal class NpgSQLConfiguratorState : IConfigurationChangedEventHandler
    {
        public NpgSQLConfiguratorState(ISecretsStore store, IConfiguration configuration) {
            _store = store;
            _configuration = configuration;
        }
        private readonly object _lock = new object();
        private readonly ISecretsStore _store;
        private readonly IConfiguration _configuration;

        public Task<NpgsqlDataSource?> GetDataSource() 
        { 
            lock(_lock)
            {
                if(_dataSourceTask ==null)
                {
                    async Task<NpgsqlDataSource?> CreateDataSource()
                    {
                        var section = _configuration.GetValue(NpgSQLConfigurationSection.SectionPath, new NpgSQLConfigurationSection());

                        if (section.PasswordPath != null)
                        {
                            var secret = await _store.GetSecret(section.PasswordPath);

                            if (secret != null && secret.Value != null)
                            {
                                section.Password = Encoding.UTF8.GetString(secret.Value);
                            }
                        }

                        if (section.IsValid)
                        {
                            var builder = new DbConnectionStringBuilder
                            {
                                { "Host", section.Host },
                                { "Database", section.Database },
                                {"Username",section.Username },
                                {"Password",section.Password },
                                {"Maximum Pool Size",section.MaxPoolSize}
                            };

                            var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString);
                            dataSourceBuilder.UseNodaTime();

                            return dataSourceBuilder.Build();
                        }
                        else
                        {
                            return null;
                        }

                    }
                    _dataSourceTask = CreateDataSource();
                }

                return _dataSourceTask;
            }
        }

        public void OnConfigurationChanged()
        {
            async Task DisposeAsync(Task<NpgsqlDataSource?>? task)
            {
                if(task!=null)
                {
                    using (await task)
                    {
                    }
                }
            }
            lock(_lock)
            {
                _ = DisposeAsync(_dataSourceTask);
                _dataSourceTask = null;
            }
           
        }

        private Task<NpgsqlDataSource?>? _dataSourceTask;
    }
    internal class NpgSQLConfigurator : IDbContextLifecycleHandler
    {
        private readonly NpgSQLConfiguratorState _state;

        public NpgSQLConfigurator(NpgSQLConfiguratorState state)
        {
            
            _state = state;
        }
        public void OnConfiguring(DbContextOptionsBuilder optionsBuilder, string contextId, Dictionary<string, object> customData)
        {
            if (customData.TryGetValue("npgsql", out var npgsqlData) && npgsqlData is NpgsqlDataSource source)
            {
               
                optionsBuilder
                    .UseNpgsql(source, o => o.UseNodaTime())
                    .UseSnakeCaseNamingConvention();
                    
            }
        }

        public async Task OnPreInit(InitializeDbContext ctx)
        {
            var source = await _state.GetDataSource();
            if(source != null)
            {
                ctx.CustomData["npgsql"] = source;
            }
           
            


        }
    }
}