
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.GameServers.Agent
{
    internal class DockerService : IDisposable, IProgress<Message>
    {
        private object _lock = new object();

        private DockerClient _docker;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly PortsManager _portsManager;
        private readonly Messager _messager;
        private readonly DockerAgentConfigurationOptions _options;

        public DockerService(
            ILogger<DockerService> logger, 
            IConfiguration configuration, 
            PortsManager portsManager, 
            Messager messager)
        {
            var dockerConfig = new DockerClientConfiguration();


            _docker = dockerConfig.CreateClient();
            _logger = logger;
            _configuration = configuration;
            _portsManager = portsManager;
            _messager = messager;
            _options = new DockerAgentConfigurationOptions();

            _configuration.Bind(_options.Section, _options);
            _ = StartMonitorDocker();
        }
        public async Task<bool> IsDockerRunning()
        {
            try
            {
                await _docker.System.PingAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private Dictionary<string, ServerContainer> _trackedContainers = new Dictionary<string, ServerContainer>();

        public Action<ServerContainerStateChange>? OnContainerStateChanged { get; set; }

        public async Task<ServerContainer> StartContainer(
            string image,
            string name,
            Dictionary<string, string> labels,
            Dictionary<string, string> environmentVariables,
            long memory,
            long cpuCount)
        {
            _logger.Log(LogLevel.Information, "Starting docker container {name} from image '{image}'.", name, image);
            var images = await _docker.Images.ListImagesAsync(new ImagesListParameters { All = true });
            if (!images.Any(i => i.RepoTags.Contains(image)))
            {
                _logger.Log(LogLevel.Information, "Downloading image {name}...", image);
                await _docker.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = image }, new AuthConfig { }, NullDockerJsonMessageProgress.Instance);
                _logger.Log(LogLevel.Information, "Image {name} downloaded.", image);
            }


            var publicIp = _options.PublicIp;
            var portReservation = _portsManager.AcquirePort();
            CreateContainerParameters parameters = new CreateContainerParameters()
            {
                Image = image,
                Name = name,
                Labels = labels,
                HostConfig = new HostConfig()
                {

                    DNS = new[] { "8.8.8.8", "8.8.4.4" },//Use Google DNS. 
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        [portReservation.Port + "/udp"] = new List<PortBinding>
                        {
                            new PortBinding
                            {
                                HostIP = publicIp,
                                HostPort = portReservation.Port.ToString()
                            }
                        }
                    },
                    Memory = memory,
                    CPUCount = cpuCount

                },

                ExposedPorts = new Dictionary<string, EmptyStruct> { { portReservation.Port + "/udp", new EmptyStruct() } },
                Env = environmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),

            };

            _logger.Log(LogLevel.Information, "Creating docker container from image {image}.", image);

            var response = await _docker.Containers.CreateContainerAsync(parameters);

            _logger.Log(LogLevel.Information, "Starting docker container {id} from image {image}.", response.ID, image);

            var container = new ServerContainer(response.ID, new List<string> { name }, image, DateTime.UtcNow);



            _trackedContainers.Add(response.ID, container);

            var startResponse = await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters { });

            return container;

        }

        public async Task StopContainer(string id, uint waitBeforeKillSeconds)
        {

            await _docker.Containers.StopContainerAsync(id, new Docker.DotNet.Models.ContainerStopParameters { WaitBeforeKillSeconds = waitBeforeKillSeconds });

        }

        public async IAsyncEnumerable<ServerContainer> ListContainers()
        {
            var response = await _docker.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters { All = true });

            foreach (var container in response)
            {
                yield return new ServerContainer(container.ID, container.Names, container.Image, container.Created);
            }
        }

        private bool _shouldMonitorDocker = true;
        private DateTime _monitorSince;
        private async Task StartMonitorDocker()
        {
            while (_docker != null)
            {
                await Task.Delay(1000);

                if (!_shouldMonitorDocker)
                {
                    return;
                }
                try
                {
                    await _docker.System.MonitorEventsAsync(new ContainerEventsParameters { Since = ((int)(_monitorSince - DateTime.UnixEpoch).TotalSeconds).ToString() }, this);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, "an error occured while querying docker for events : {ex}", ex);
                    await Task.Delay(10000);
                }
            }
        }


        public void Dispose()
        {
            _shouldMonitorDocker = false;
            _docker?.Dispose();
        }

        public void Report(Message value)
        {
            _monitorSince = DateTime.UtcNow;
            if (value.Status == "stop")
            {

                if (_trackedContainers.Remove(value.ID, out var server))
                {
                    using (server)
                    {

                        _logger.Log(LogLevel.Information, "Docker container {id} stopped.", value.ID);

                        _messager.PostServerStoppedMessage(server);

                    }
                }
            }
        }
    }

    public class ServerContainer : IDisposable
    {
        internal ServerContainer(string id, IList<string> names, string image, DateTime created)
        {
            Id = id;
            Names = names;
            Image = image;
            Created = created;
        }

        public string Id { get; }
        public IList<string> Names { get; }
        public string Image { get; }
        public DateTime Created { get; }

        public void AddResource(IDisposable resource)
        {
            _resources.Add(resource);
        }

        private List<IDisposable> _resources = new List<IDisposable>();
        public void Dispose()
        {
            foreach (var resource in _resources)
            {
                resource.Dispose();
            }
        }
    }

    public enum ServerContainerStateChangeEventType
    {
        Start,
        Stop
    }
    public class ServerContainerStateChange
    {
        public ServerContainer Container { get; set; }
    }
}
