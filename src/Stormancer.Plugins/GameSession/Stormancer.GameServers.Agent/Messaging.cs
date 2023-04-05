using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class Messager
    {
      
        private readonly IServiceScopeFactory serviceScopeFactory;

        public Messager(IServiceScopeFactory serviceScopeFactory)
        {
          
            this.serviceScopeFactory = serviceScopeFactory;
        }
        public void PostServerStoppedMessage(ServerContainer container)
        {
            using(var scope  = serviceScopeFactory.CreateScope())
            {
                var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IMessageHandler>>();
                foreach (var handler in handlers)
                {
                    handler.OnServerStopped(container);
                }
            }
           
        }

        public void PostServerStartedMessage(ServerContainer container)
        {
            using (var scope = serviceScopeFactory.CreateScope())
            {
                var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IMessageHandler>>();
                foreach (var handler in handlers)
                    handler.OnServerStarted(container);
            }
        }
    }

    internal interface IMessageHandler
    {
        Task OnServerStarted(ServerContainer container);
        Task OnServerStopped(ServerContainer container);
    }
}
