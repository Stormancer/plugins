using Newtonsoft.Json.Linq;
using Stormancer.Core;
using Stormancer.Server.Plugins.API;
using Stormancer.Server.Plugins.Queries;
using Stormancer.Server.Plugins.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    internal class RemoteControlController : ControllerBase
    {
        private readonly RemoteControlService service;
        private readonly IUserSessions userSessions;

        public RemoteControlController(RemoteControlService service, IUserSessions userSessions)
        {
            this.service = service;
            this.userSessions = userSessions;
        }
        protected override async Task OnConnected(IScenePeerClient peer)
        {
            var session = await userSessions.GetSession(peer, System.Threading.CancellationToken.None);

           
            if (session!=null && session.platformId.Platform == RemoteControlConstants.AUTHPROVIDER_TYPE)
            {
                service.ConnectAgent(session);
            }
            
        }

        protected override Task OnDisconnected(DisconnectedArgs args)
        {
            service.DisconnectAgent(args.Peer.SessionId);
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<AgentCommandOutputEntry> RunCommand(string command, IEnumerable<SessionId> sessionIds, CancellationToken cancellationToken)
        {
            return service.RunCommandAsync(command, sessionIds, cancellationToken);
        }

        public SearchResult<Agent> SearchAgents(string query,uint size, uint skip)
        {
            return service.SearchAgents(JObject.FromObject(query), size, skip);
        }

    }
}
