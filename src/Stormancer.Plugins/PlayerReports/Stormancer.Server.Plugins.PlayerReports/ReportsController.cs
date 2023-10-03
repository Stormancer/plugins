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
        private readonly ISerializer _serializer;

        public ReportsController(ReportsService reports, IUserSessions sessions, ISerializer serializer)
        {
            _reports = reports;
            _sessions = sessions;
            _serializer = serializer;
        }

        protected override Task OnConnected(IScenePeerClient peer)
        {
            return base.OnConnected(peer);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task CreatePlayerReport(RequestContext<IScenePeerClient> ctx)
        {
            var session = await _sessions.GetSession(ctx.RemotePeer,ctx.CancellationToken);
            string targetUserId = ctx.ReadObject<string>();
            string message = ctx.ReadObject<string>();
            JObject customData = ctx.ReadObject<JObject>();

            await _reports.CreatePlayerReportAsync(session.User.Id, targetUserId, message, customData, ctx.CancellationToken);
        }

    }
}