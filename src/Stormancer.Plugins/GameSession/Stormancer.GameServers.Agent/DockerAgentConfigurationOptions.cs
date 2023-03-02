using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    public class DockerAgentConfigurationOptions
    {
        public string Section => "agent";
        public string? PublicIp { get; set; }

        public ushort MinPort { get; set; } = 40000;
        public ushort MaxPort { get; set; } = 40050;

        public string? StormancerEndpoint { get; set; }
        public string? StormancerAccount { get; set; }
        public string? StormancerApplication { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? PrivateKeyPassword { get; set; }

        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public uint MaxMemory { get; set; }
        public uint MaxCpu { get; set; }
        public string Id { get; set; } = Environment.MachineName;

        public ConfigurationValidationModel Validate()
        {
            var model = new ConfigurationValidationModel();
            model.Success = true;

            if(StormancerEndpoint == null)
            {
                model.Error($"'agent.{nameof(StormancerEndpoint)}' missing.");
            }

            if (StormancerAccount == null)
            {
                model.Error($"'agent.{nameof(StormancerAccount)}' missing.");
            }

            if (StormancerApplication == null)
            {
                model.Error($"'agent.{nameof(StormancerApplication)}' missing.");
            }

            if (PrivateKeyPath == null)
            {
                model.Error($"'agent.{nameof(PrivateKeyPath)}' missing.");
            }

            return model;


        }

    }

    
}
