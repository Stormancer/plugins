using Newtonsoft.Json.Linq;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.PlayerReports
{
    internal class ReportsController : ControllerBase
    {
        private readonly ReportsService _reports;
        private readonly IUserSessions _sessions;

        public ReportsController(ReportsService reports, IUserSessions sessions)
        {
            _reports = reports;
            _sessions = sessions;
        }

        public async Task CreatePlayerReport(string targetUserId,string message, JObject customData, RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer,ctx.CancellationToken);
            await _reports.CreatePlayerReportAsync(session.User.Id, targetUserId, message, customData, ctx.CancellationToken);
        }

        public Task CreateBugReport(string message, JObject customData, RequestContext<IScenePeerClient> ctx)
        {
            throw new ClientException("notImplemented");
        }
    }
}