using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Plugins.Configuration;
using Stormancer.Server.Plugins.Database.EntityFrameworkCore;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    /// <summary>
    /// Configuration section related to the bug reporting system.
    /// </summary>
    public class BugReportsConfigurationSection : IConfigurationSection<BugReportsConfigurationSection>
    {

        ///<inheritdoc/>
        public static string SectionPath { get; } = "bugReports";

        ///<inheritdoc/>
        public static BugReportsConfigurationSection Default { get; } = new BugReportsConfigurationSection();


        /// <summary>
        /// Id of the backend to use to store bug reports.
        /// </summary>
        public string? Backend { get; set; }
    }

    /// <summary>
    /// Provides API to create player reports.
    /// </summary>
    public class ReportsService
    {
        private readonly DbContextAccessor _dbContextAccessor;
        private readonly IEnumerable<IBugReportingBackend> _backends;
        private readonly ILogger _logger;
        private readonly ConfigurationMonitor<BugReportsConfigurationSection> _configuration;

        /// <summary>
        /// Creates a new instance of <see cref="ReportsService"/>
        /// </summary>
        /// <param name="dbContextAccessor"></param>
        /// <param name="backends"></param>
        /// <param name="logger"></param>
        /// <param name="configuration"></param>
        public ReportsService(DbContextAccessor dbContextAccessor, IEnumerable<IBugReportingBackend> backends, ILogger logger,ConfigurationMonitor<BugReportsConfigurationSection> configuration)
        {
            _dbContextAccessor = dbContextAccessor;
            _backends = backends;
            _logger = logger;
            _configuration = configuration;
        }
        internal async Task CreatePlayerReportAsync(string reporterUserId, string reportedUserId, string message, JObject customData, CancellationToken cancellationToken)
        {
            var ctx = await _dbContextAccessor.GetDbContextAsync();

            var usersSet = ctx.Set<UserRecord>();
            var reporter = await usersSet.FindAsync(Guid.Parse(reporterUserId));
            var reported = await usersSet.FindAsync(Guid.Parse(reportedUserId));
            if (reporter == null)
            {
                throw new InvalidOperationException($"user {reporterUserId} not found.");
            }

            if (reported == null)
            {
                throw new InvalidOperationException($"user {reportedUserId} not found.");
            }

            var set = ctx.Set<PlayerReport>();

            await set.AddAsync(new PlayerReport { Context = JsonDocument.Parse(customData.ToString()), Message = message, Reporter = reporter, Reported = reported, CreatedOn = DateTime.UtcNow });

            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Saves a bug report
        /// </summary>
        /// <param name="reporterId"></param>
        /// <param name="message"></param>
        /// <param name="customData"></param>
        /// <param name="attachments"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task SaveBugReportAsync(string reporterId, string message, JObject customData, IEnumerable<BugReportAttachmentContent> attachments, CancellationToken cancellationToken)
        {
            var config = _configuration.Value;
            if (config.Backend == null)
            {
                return;
            }

            foreach (var backend in _backends)
            {
                if (backend.Type == config.Backend)
                {
                    try
                    {
                        await backend.ProcessBugReportAsync(reporterId, message, customData, attachments, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "bugReports", "An error occurred while reporting a bug.", ex);
                    }
                }
            }
        }


    }
}
