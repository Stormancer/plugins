using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Regions
{
    public class RegionsTestingService
    {
        private readonly IUserSessions _sessions;
        private readonly AgentServerProxy _agentServer;

        public RegionsTestingService(IUserSessions sessions, AgentServerProxy agentServer)
        {
            _sessions = sessions;
            _agentServer = agentServer;
        }
        public async ValueTask<Dictionary<string, string>> GetTestIps()
        {
            var agents =await  _agentServer.GetTestIps();


        }

        public Task UpdateRegionAsync(SessionId sessionId, string? regionName, CancellationToken cancellationToken)
        {
            return _sessions.UpdateSessionData(sessionId, "stormancer.region", Encoding.UTF8.GetBytes(regionName??string.Empty), cancellationToken);

        }
        public string GetRegion(Session session)
        {
            if(session.SessionData.TryGetValue("stormancer.region",out var utf8))
            {
                return Encoding.UTF8.GetString(utf8);
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
