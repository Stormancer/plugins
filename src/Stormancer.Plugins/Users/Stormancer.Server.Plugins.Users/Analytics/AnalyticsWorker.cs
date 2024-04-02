using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Analytics
{
    internal class SessionsAnalyticsWorker
    {
        private readonly IAnalyticsService _analytics;
        private readonly UserSessions _userSessions;

        public SessionsAnalyticsWorker(IAnalyticsService service, IUserSessions userSessions)
        {
            _analytics = service;
            _userSessions = (UserSessions)userSessions;
        }


        public async Task Run(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            while (!cancellationToken.IsCancellationRequested)
            {

                await timer.WaitForNextTickAsync(cancellationToken);
                try
                {
                    var groups = await _userSessions.GetAuthenticatedUsersByDimensionsAsync();
                    foreach (var group in groups)
                    {
                        _analytics.Push("user", "sessions",
                            JObject.FromObject(group)
                            );
                    }
                }
                catch { }


            }
        }
    }
}
