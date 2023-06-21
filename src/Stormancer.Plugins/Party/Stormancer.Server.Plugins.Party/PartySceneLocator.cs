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
                    if (!string.IsNullOrEmpty(ctx.ServiceName) )
                    {
                        //On WJ2, There is a bug in the Client Switch plugin. It requests a scene token by providing the scene id as 
                        //the service name instead of using the partyId (as it should). If the serviceName already starts with 'party-',
                        //we know that it's not a party id, but the target sceneId and we shouldn't try to translate it.
                        if (!ctx.ServiceName.StartsWith("party-"))
                        {
                            ctx.SceneId = "party-" + ctx.ServiceName;
                        }
                        else
                        {
                            ctx.SceneId = ctx.ServiceName;
                        }
                    }
                    
                  
                    break;
                default:
                    break;
        }
            return Task.CompletedTask;
        }
    }
}
