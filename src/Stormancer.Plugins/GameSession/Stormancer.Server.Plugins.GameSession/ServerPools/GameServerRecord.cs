using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    public class GameServerRecord
    {
        public string Id { get; set; }

        public string Pool { get; set; }

        public JObject CustomData { get; set; }

        public DateTime StartedOn { get; set; }
        public DateTime ClosedOn { get; set; }
    }
}
