using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    public class GameServerEvent
    {
        public string Id { get; set; }
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
 
        public JObject CustomData { get; set; } = new JObject();
    }
}
