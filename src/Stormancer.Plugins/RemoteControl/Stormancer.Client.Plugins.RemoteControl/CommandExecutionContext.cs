using Newtonsoft.Json.Linq;
using Stormancer.Core;

namespace Stormancer.Plugins.RemoteControl
{
    public class CommandExecutionContext
    {
        private string[] segments;
        private RequestContext<IScenePeer> ctx;

        internal CommandExecutionContext(string[] segments, RequestContext<IScenePeer> ctx)
        {
            this.segments = segments;
            this.ctx = ctx;
        }

        public string[] CommandSegments => segments;
        public void SendResult(string type, JObject content)
        {
            ctx.SendValue(new AgentCommandOutputEntryDto { Type = type, ResultJson = content.ToString() });
        }

        public void SendResult<T>(string type, T content)
        {
            ctx.SendValue(new AgentCommandOutputEntryDto { Type = type, ResultJson = JObject.FromObject(content).ToString() });
        }

        public void SendResult(string type, string json)
        {
            ctx.SendValue(new AgentCommandOutputEntryDto { Type = type, ResultJson = json});
        }
    }
}