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
            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            string targetUserId = ctx.ReadObject<string>();
            string message = ctx.ReadObject<string>();
            JObject customData = ctx.ReadObject<JObject>();

            await _reports.CreatePlayerReportAsync(session.User.Id, targetUserId, message, customData, ctx.CancellationToken);
        }

        [Api(ApiAccess.Public, ApiType.Rpc)]
        public async Task CreateBugReport(string message, JObject customData, string contentType, int length, RequestContext<IScenePeerClient> ctx)
        {
            if (length > 50 * 1024)
            {
                throw new ClientException($"contentToBig?maxSize=5120050&actualSize={length}");
            }

            var session = await _sessions.GetSession(ctx.RemotePeer, ctx.CancellationToken);
            if (session == null || session.User == null)
            {
                throw new ClientException("notAuthenticated");
            }

            using var owner = MemoryPool<byte>.Shared.Rent(length);
            var mem = owner.Memory.Slice(0, length);
            ctx.InputStream.Read(mem.Span);
            var list = new List<BugReportAttachmentContent> { new BugReportAttachmentContent(contentType, "log", mem) };
            await _reports.SaveBugReportAsync(session.User.Id, message, customData, list);
        }

    }
}