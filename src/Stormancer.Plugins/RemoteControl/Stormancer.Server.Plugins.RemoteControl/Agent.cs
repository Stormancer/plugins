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
        public SessionId SessionId { get; set; }

        public Newtonsoft.Json.Linq.JObject Metadata { get; set; }

        public string Name { get; set; }

    }
}
