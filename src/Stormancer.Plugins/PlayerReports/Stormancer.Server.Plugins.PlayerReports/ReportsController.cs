using Newtonsoft.Json.Linq;
using Stormancer.Plugins;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Users;
using System.Buffers;
using System.Collections.Generic;
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

        protected override Task OnConnected(IScenePeerClient peer)
        {
            return base.OnConnected(peer);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task CreatePlayerReport(RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            string targetUserId = ctx.ReadObject<string>();
            string message = ctx.ReadObject<string>();
            JObject customData = ctx.ReadObject<JObject>();

            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }


            await _reports.CreatePlayerReportAsync(session.User.Id, targetUserId, message, customData, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task CreateBugReport(string message, JObject customData, RequestContext<IScenePeerClient> ctx)
        {


            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }


            var list = new List<BugReportAttachmentContent> { };
            await _reports.SaveBugReportAsync(session.User.Id, message, customData, list, ctx.CancellationToken);

        }

    }
}