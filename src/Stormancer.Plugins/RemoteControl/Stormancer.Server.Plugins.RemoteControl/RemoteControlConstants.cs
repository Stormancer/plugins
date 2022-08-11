using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    /// <summary>
    /// Constants for the remote control plugin.
    /// </summary>
    public static class RemoteControlConstants
    {
        /// <summary>
        /// Type of auth provider
        /// </summary>
        public const string AUTHPROVIDER_TYPE = "remoteControl.agent";

        public const string AUTHENTICATION_KEYS_AGENTID = "id";
        public const string AUTHENTICATION_KEYS_PASSWORD = "password";

        public const string AUTHENTICATION_KEYS_METADATA = "metadata";
    }
}
