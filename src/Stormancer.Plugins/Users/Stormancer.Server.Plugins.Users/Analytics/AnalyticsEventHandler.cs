using Newtonsoft.Json.Linq;
using Stormancer.Server.Plugins.Analytics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Users.Analytics
{
    [Priority(int.MinValue)]
    class AnalyticsEventHandler : IUserSessionEventHandler
    {
        private readonly IAnalyticsService analytics;

        public AnalyticsEventHandler(IAnalyticsService analytics)
        {
            this.analytics = analytics;
        }
        public Task OnLoggedIn(LoginContext loginCtx)
        {
            analytics.Push("user", "login", JObject.FromObject(new { SessionId = loginCtx.Session.SessionId.ToString(), UserId = loginCtx.Session.User?.Id, PlatformId = loginCtx.Session.platformId,  loginCtx.Session.Dimensions }));
            return Task.CompletedTask;
        }

        public Task OnLoggedOut(LogoutContext logoutCtx)
        {
            analytics.Push("user", "logout", JObject.FromObject(new { SessionId = logoutCtx.Session.SessionId.ToString(), UserId = logoutCtx.Session.User?.Id, logoutCtx.ConnectedOn, duration = (DateTime.UtcNow - logoutCtx.ConnectedOn).TotalSeconds }));
            return Task.CompletedTask;
        }
    }
}
