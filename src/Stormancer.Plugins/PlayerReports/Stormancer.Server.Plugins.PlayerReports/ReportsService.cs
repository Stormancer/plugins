﻿using Newtonsoft.Json.Linq;
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
    internal class ReportsService
    {
        private readonly DbContextAccessor _dbContextAccessor;

        public ReportsService(DbContextAccessor dbContextAccessor)
        {
            _dbContextAccessor = dbContextAccessor;
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

        internal async Task CreateBugReportAsync(string reporterUserId, string message, JObject customData,string contentType, ReadOnlyMemory<byte> data)
        {

        }
    }
}
