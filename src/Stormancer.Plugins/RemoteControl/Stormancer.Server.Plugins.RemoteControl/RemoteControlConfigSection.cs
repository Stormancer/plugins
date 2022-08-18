using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    /// <summary>
    /// Configuration section for the remote control plugin.
    /// </summary>
    /// <remarks>
    /// {
    ///     ...
    ///     "remoteControl":{
    ///         ...
    ///     }
    ///     ...
    /// }
    /// 
    /// </remarks>
    public class RemoteControlConfigSection
    {
        /// <summary>
        /// Path in the secrets store to the agents auth secret.
        /// </summary>
        public string? AgentKeyPath { get; set; }
    }
}
