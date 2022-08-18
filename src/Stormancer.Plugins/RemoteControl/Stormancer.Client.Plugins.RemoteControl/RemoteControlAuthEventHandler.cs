using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins.RemoteControl
{
    internal class RemoteControlledAgentAuthEventHandler : Stormancer.Plugins.IAuthenticationEventHandler
    {
        private readonly RemoteControlConfiguration config;

        public RemoteControlledAgentAuthEventHandler(RemoteControlConfiguration config)
        {
            this.config = config;
        }


        public Task RetrieveCredentials(CredentialsContext ctx)
        {
            ctx.AuthParameters.Type = "remoteControl.agent";
            ctx.AuthParameters.Parameters["id"] = config.Id;
            ctx.AuthParameters.Parameters["password"] = config.Password;
            ctx.AuthParameters.Parameters["metadata"] = config.Metadata;

            return Task.CompletedTask;
        }
    }
}
