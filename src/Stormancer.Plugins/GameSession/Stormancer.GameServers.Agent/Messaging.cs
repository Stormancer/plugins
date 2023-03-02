using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class Messager
    {
        private readonly Func<IEnumerable<IMessageHandler>> _handlers;

        public Messager(Func<IEnumerable<IMessageHandler>> handlers)
        {
            _handlers = handlers;
        }
        public void PostServerStoppedMessage(ServerContainer container)
        {
            foreach(var handler in _handlers())
            {
                handler.OnServerStopped(container);
            }
        }

        public void PostServerStartedMessage(ServerContainer container)
        {
            foreach (var handler in _handlers())
            {
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
