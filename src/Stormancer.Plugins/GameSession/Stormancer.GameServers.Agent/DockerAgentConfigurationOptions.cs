using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    public class ApplicationConfigurationOptions : IEquatable<ApplicationConfigurationOptions>
    {
        public override string ToString()
        {
            return $"{StormancerEndpoint}/{StormancerAccount}/{StormancerApplication}";
        }

        public bool Equals(ApplicationConfigurationOptions? other)
        {
            if(other == null) return false ;
            if(StormancerEndpoint!=other.StormancerEndpoint) return false ;
            if(StormancerAccount!= other.StormancerAccount) return false ;
            if(StormancerApplication!=other.StormancerApplication) return false ;

            return true;
        }

        public string? StormancerEndpoint { get; set; }
        public string? StormancerAccount { get; set; }
        public string? StormancerApplication { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? PrivateKeyPassword { get; set; }
    }
    public class DockerAgentConfigurationOptions
    {
        public string Section => "agent";
        public string? PublicIp { get; set; }

        public ushort MinPort { get; set; } = 40000;
        public ushort MaxPort { get; set; } = 40050;

        public uint MaxMemory { get; set; }
        public float MaxCpu { get; set; }
        public string Id { get; set; } = Environment.MachineName;

        public Dictionary<string, ApplicationConfigurationOptions> Applications { get; set; }
       

        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

      

        public ConfigurationValidationModel Validate()
        {
            var model = new ConfigurationValidationModel();
            model.Success = true;


            return model;


        }

    }

    
}
