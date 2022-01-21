using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Analytics
{
    class AnalyticsEventHandler : IUserSessionEventHandler
    {
        private readonly IAnalyticsService analytics;

        public AnalyticsEventHandler(IAnalyticsService analytics)
        {
            this.analytics = analytics;
        }
        public Task OnLoggedIn(LoginContext loginCtx)
        {
            analytics.Push("user", "login", JObject.FromObject(new { SessionId = loginCtx.Session.SessionId, UserId = loginCtx.Session.User?.Id, PlatformId = loginCtx.Session.platformId }));
            return Task.CompletedTask;
        }

        public Task OnLoggedOut(LogoutContext logoutCtx)
        {
            analytics.Push("user", "logout", JObject.FromObject(new { SessionId = logoutCtx.Session.SessionId, UserId = logoutCtx.Session.User?.Id, logoutCtx.ConnectedOn, duration = (DateTime.UtcNow - logoutCtx.ConnectedOn).TotalSeconds }));
            return Task.CompletedTask;
        }
    }
}
