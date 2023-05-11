using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stormancer.Server.Plugins.GameSession.ServerProviders;

namespace Stormancer.Server.Plugins.Regions
{
    public class RegionsTestingService
    {
        private readonly IUserSessions _sessions;
        private readonly AgentServerProxy _agentServer;
        private readonly RegionTestingStorage _storage;
        private readonly ISerializer _serializer;

        public RegionsTestingService(IUserSessions sessions, AgentServerProxy agentServer, RegionTestingStorage storage, ISerializer serializer)
        {
            _sessions = sessions;
            _agentServer = agentServer;
            _storage = storage;
            _serializer = serializer;
        }
        public async Task<Dictionary<string, string>> GetTestIps()
        {
            var result = await _storage.Cache.Get(0, async _ => await _agentServer.GetRegions(CancellationToken.None), TimeSpan.FromMinutes(1));
            return result ?? new();
        }

        public Task UpdateRegionAsync(SessionId sessionId, IEnumerable<string> regions, CancellationToken cancellationToken)
        {
            return _sessions.UpdateSessionData(sessionId, "stormancer.region", regions, cancellationToken);

        }
        public IEnumerable<string> GetRegion(Session session)
        {
            return session.GetSessionValue<IEnumerable<string>>("stormancer.region", _serializer) ?? Enumerable.Empty<string>();

        }
    }
}
