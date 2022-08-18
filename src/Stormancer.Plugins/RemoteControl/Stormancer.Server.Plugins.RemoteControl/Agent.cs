using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.RemoteControl
{
    /// <summary>
    /// Remote controlled agent
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="name"></param>
        /// <param name="metadata"></param>
        public Agent(SessionId sessionId, string name, JObject metadata)
        {
            SessionId = sessionId;
            Name = name;
            Metadata = metadata;
        }

        /// <summary>
        /// <see cref="Stormancer.SessionId"/> of the agent.
        /// </summary>
        public SessionId SessionId { get;  }

        /// <summary>
        /// Metadata associated with the agent.
        /// </summary>
        public JObject Metadata { get; }

        /// <summary>
        /// Name of the agent.
        /// </summary>
        public string Name { get;  }

        /// <summary>
        /// Command currently running on the agent. Null if none.
        /// </summary>
        public AgentRunningCommand? RunningCommand { get; set; }

    }

    /// <summary>
    /// A command running on the agent.
    /// </summary>
    /// <param name="Uid"></param>
    /// <param name="Command"></param>
    /// <param name="StartTime"></param>
    public record AgentRunningCommand(string Uid, string Command, DateTime StartTime);
   
}
