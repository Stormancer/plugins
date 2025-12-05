using Stormancer.Server.Plugins.Users;
using Stormancer.Server.Plugins.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Steam
{
    internal class SteamNetworkingEventHandler : IP2pEventHandler
    {
        private readonly IUserSessions _sessions;

        public SteamNetworkingEventHandler(IUserSessions sessions)
        {
            _sessions = sessions;
        }
        public async ValueTask OnGetP2PMetadata(OnGetP2PMetadataContext ctx)
        {
            var session = await _sessions.GetSession(ctx.Target, CancellationToken.None);
            if (session != null && session.User!=null && session.User.TryGetSteamId(out var steamId))
            {
                ctx.Metadata["steam"] = steamId.ToString();
            }
        }
    }
}
