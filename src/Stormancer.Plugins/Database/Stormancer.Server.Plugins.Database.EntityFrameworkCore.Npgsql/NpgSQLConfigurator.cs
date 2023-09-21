using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Secrets;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Database.EntityFrameworkCore.Npgsql
{
    public class NpgSQLConfigurationSection
    {
        /// <summary>
        /// Gets the path of the Npgsql section in the configuration.
        /// </summary>
        public const string SECTION_PATH = "npgsql";

        /// <summary>
        /// Gets or sets the host of the postgresql server.
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
    }

    internal class NpgSQLConfigurator : IDbContextLifecycleHandler
    {
        private readonly ISecretsStore _store;
        private readonly IConfiguration _configuration;

        public NpgSQLConfigurator(ISecretsStore store, IConfiguration configuration)
        {
            _store = store;
            _configuration = configuration;
        }
        public void OnConfiguring(DbContextOptionsBuilder optionsBuilder, string contextId, Dictionary<string, object> customData)
        {
            if (customData.TryGetValue("npgsql", out var npgsqlData) && npgsqlData is NpgSQLConfigurationSection config && config.IsValid)
            {
                var builder = new DbConnectionStringBuilder
                {
                    { "Host", config.Host },
                    { "Database", config.Database },
                    {"Username",config.Username },
                    {"Password",config.Password }
                };

                var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString);
                dataSourceBuilder.UseNodaTime();
               
                var dataSource = dataSourceBuilder.Build();
                optionsBuilder
                    .UseNpgsql(dataSource, o => o.UseNodaTime())
                    .UseSnakeCaseNamingConvention();
                    
            }
        }

        public async Task OnPreInit(InitializeDbContext ctx)
        {
            var section = _configuration.GetValue(NpgSQLConfigurationSection.SECTION_PATH, new NpgSQLConfigurationSection());

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
                ctx.CustomData["npgsql"] = section;
            }


        }
    }
}