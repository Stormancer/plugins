using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class PortsManager
    {
        private readonly DockerAgentConfigurationOptions _options;

        public PortsManager(IConfiguration configuration)
        {

            _options = new DockerAgentConfigurationOptions();
            configuration.Bind(DockerAgentConfigurationOptions.Section, _options);
        }

        private HashSet<ushort> _acquiredPorts = new HashSet<ushort>();
        private object _syncRoot = new object();
        public PortReservation AcquirePort()
        {
            lock (_syncRoot)
            {
                for (ushort port = (ushort)_options.MinPort; port <= _options.MaxPort; port++)
                {
                    if (!_acquiredPorts.Contains(port))
                    {
                        _acquiredPorts.Add(port);
                        return new PortReservation(port, this);
                    }
                }
            }

            throw new InvalidOperationException("No port available.");
        }
        public void ReleasePort(ushort port)
        {
            lock (_syncRoot)
            {
                _acquiredPorts.Remove(port);
            }
        }
    }

    public class PortReservation : IDisposable
    {

        private readonly PortsManager _portsManager;

        internal PortReservation(ushort port, PortsManager portsManager)
        {
            Port = port;
            _portsManager = portsManager;

        }
        public ushort Port { get; }
        public void Dispose()
        {
            _portsManager.ReleasePort(Port);

        }
    }
}
