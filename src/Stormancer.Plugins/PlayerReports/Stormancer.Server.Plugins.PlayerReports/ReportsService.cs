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
    public class BugReportsConfigurationSection
    {
        internal const string PATH = "bugReports";


        /// <summary>
        /// Id of the backend to use to store bug reports.
        /// </summary>
        public string? Backend { get; set; }
    }
    internal class ReportsService
    {
        private readonly DbContextAccessor _dbContextAccessor;
        private readonly IEnumerable<IBugReportingBackend> _backends;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public ReportsService(DbContextAccessor dbContextAccessor, IEnumerable<IBugReportingBackend> backends, ILogger logger, IConfiguration configuration)
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

        public async Task SaveBugReportAsync(string reporterId, string message, JObject customData, IEnumerable<BugReportAttachmentContent> attachments, CancellationToken cancellationToken)
        {
            var config = _configuration.GetValue<BugReportsConfigurationSection>("bugReports") ?? new BugReportsConfigurationSection();
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
