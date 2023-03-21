using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    public class ConfigurationValidationModel
    {
        public bool Success { get; set; } = true;

        public void Error(string message)
        {
            Success= false;
            Errors.Add(message);
        }

        public List<string> Errors { get; } = new List<string>();
    }
}
