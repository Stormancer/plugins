using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    class NullDockerJsonMessageProgress : IProgress<JSONMessage>
    {
        public void Report(JSONMessage value)
        {

        }

        public static NullDockerJsonMessageProgress Instance { get; } = new NullDockerJsonMessageProgress();
    }
}
