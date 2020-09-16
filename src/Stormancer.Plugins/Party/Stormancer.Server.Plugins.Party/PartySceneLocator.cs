using Stormancer.Server.Plugins.ServiceLocator;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.Party
{
    class PartySceneLocator : IServiceLocatorProvider
    {
        public Task LocateService(ServiceLocationCtx ctx)
        {
            switch (ctx.ServiceType)
            {
                case PartyPlugin.PARTY_MANAGEMENT_SERVICEID:
                    ctx.SceneId = PartyPlugin.PARTY_MANAGEMENT_SCENEID;
                    break;
                case PartyPlugin.PARTY_SERVICEID:
                    ctx.SceneId = "party-"+ctx.ServiceName;
                    break;
                default:
                    break;
        }
            return Task.CompletedTask;
        }
    }
}
