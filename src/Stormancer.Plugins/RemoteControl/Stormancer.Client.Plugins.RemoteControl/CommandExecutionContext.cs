using Newtonsoft.Json.Linq;
using Stormancer.Core;
using System;
using System.Threading;

namespace Stormancer.Plugins.RemoteControl
{
    public class CommandExecutionContext
    {
        private string[] segments;
        private readonly RequestContext<IScenePeer> ctx;
        private readonly Action<AgentCommandOutputEntryDto> sender;
       
        internal CommandExecutionContext(string[] segments,RequestContext<IScenePeer> ctx, Action<AgentCommandOutputEntryDto> sender)
        {
            this.segments = segments;
            this.ctx = ctx;
            this.sender = sender;
           
        }

        public string[] CommandSegments => segments;
        public void SendResult(string type, JObject content)
        {
            sender(new AgentCommandOutputEntryDto { Type = type, ResultJson = content.ToString() });
        }

        public void SendResult<T>(string type, T content)
        {
            sender(new AgentCommandOutputEntryDto { Type = type, ResultJson = JObject.FromObject(content).ToString() });
        }

        public void SendResult(string type, string json)
        {
            sender(new AgentCommandOutputEntryDto { Type = type, ResultJson = json});
        }

        /// <summary>
        /// Gets the request's associated cancellation token.
        /// </summary>
        public CancellationToken CancellationToken => ctx.CancellationToken;
    }
}