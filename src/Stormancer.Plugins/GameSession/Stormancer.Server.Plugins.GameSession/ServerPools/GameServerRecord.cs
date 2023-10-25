using Nest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    public class GameSessionEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
 
        public JObject CustomData { get; set; } = new JObject();
        public string GameSessionId { get; set; }

        public string Type { get; set; }
    }
}
