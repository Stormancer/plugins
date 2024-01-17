
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.Extensions.Configuration;
using MsgPack.Serialization;
using Newtonsoft.Json;
using RakNet;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Stormancer.GameServers.Agent
{
    public enum ContainerEventType
    {
        Start,
        Stop
    }
    public class StartContainerResult
    {
        public bool Success { get; set; }

        public ServerContainer? Container { get; set; }
        public string? Error { get; set; }
    }
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

            _configuration.Bind(DockerAgentConfigurationOptions.Section, _options);
            _ = StartMonitorDocker();
        }



        public string AgentId => _options.Name;
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

        public async Task StartAgent(CancellationToken cancellationToken)
        {
            if (IsRunning)
            {
                return;
            }
            while (!await IsDockerRunning() && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
                _logger.LogError("Failed to contact the docker daemon.");
            }
            _logger.LogInformation("Docker agent found.");
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var containers = await _docker.Containers.ListContainersAsync(new ContainersListParameters { All = true });
            foreach (var container in containers)
            {


                try
                {
                    if (container.State.Contains("running", StringComparison.InvariantCultureIgnoreCase))
                    {
                        //labels["stormancer.agent.clientId"] = agentId.ToString();
                        //labels["stormancer.reservedMemory"] = reservedMemory.ToString();
                        //labels["stormancer.reservedCpu"] = reservedCpu.ToString();
                        //labels["stormancer.crashReportConfig"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(crashReportConfiguration)));
                        var name = container.Names.First();
                        var reservedMemory = container.Labels.TryGetValue("stormancer.reservedMemory", out var str) ? int.TryParse(str, out var mem) ? mem : 0 : 0;
                        var reservedCpu = container.Labels.TryGetValue("stormancer.reservedCpu", out str) ? float.TryParse(str, out var cpu) ? cpu : 0 : 0;
                        var expiresOn = container.Labels.TryGetValue("stormancer.expiresOn", out str) ? DateTime.TryParse(str, out var date) ? date : DateTime.UtcNow : DateTime.UtcNow;
                        var reservedPorts = container.Labels.TryGetValue("stormancer.reservedPorts", out var reservedPortsStr) ? reservedPortsStr.Split(',').Select(s => ushort.Parse(s)) : Enumerable.Empty<ushort>();

                        _logger.Log(LogLevel.Information, "Loading running container {name}, expiresOn={expiresOn}, reservedPorts={reservedPorts}", name, expiresOn, reservedPortsStr);
                        Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration? crashReportConfiguration = null;
                        try
                        {
                            crashReportConfiguration = JsonConvert.DeserializeObject<Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration>(Encoding.UTF8.GetString(Convert.FromBase64String(container.Labels["stormancer.crashReportConfig"])));
                        }
                        catch (Exception) { }

                        var serverContainer = new ServerContainer(0, name, container.Image, container.Created, reservedMemory, reservedCpu, crashReportConfiguration ?? new Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration(), expiresOn);
                        foreach (var port in reservedPorts)
                        {
                            serverContainer.AddResource(_portsManager.AcquirePort(port));
                        }
                        _trackedContainers.Add(name, serverContainer);

                    }
                    else
                    {

                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, "An error occurred while trying to destroy container '{containerId}' state={state}, ex={ex}", container.ID, container.State, ex);
                }

            }

            _ = RunAsync(cancellationToken);

        }


        private async Task RunAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            var containersToKill = new Queue<(string id, ServerContainer container)>();
            while (!cancellationToken.IsCancellationRequested)
            {
                await timer.WaitForNextTickAsync(cancellationToken);


                lock (_lock)
                {
                    foreach (var (id, container) in _trackedContainers)
                    {
                        if (container.ExpiresOn < DateTime.UtcNow)
                        {
                            containersToKill.Enqueue((id, container));
                        }
                    }


                }

                while (containersToKill.TryDequeue(out var tuple))
                {

                    var (id, container) = tuple;
                    try
                    {
                        await StopContainer(container.AgentId, id, 10);
                    }

                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while trying to kill the expired container '{containerId}'", id);
                    }
                }

            }
        }

        private Dictionary<string, ServerContainer> _trackedContainers = new Dictionary<string, ServerContainer>();

        public long TotalMemory => _options.MaxMemory;
        public float TotalCpu => _options.MaxCpu;

        public bool IsRunning { get; private set; }
        public long UsedMemory
        {
            get
            {
                lock (_lock)
                {
                    return _trackedContainers.Sum(kvp => kvp.Value.Memory);
                }
            }
        }

        public float UsedCpu
        {
            get
            {
                lock (_lock)
                {
                    return _trackedContainers.Sum(kvp => kvp.Value.CpuQuota);
                }
            }

        }
        public Action<ServerContainerStateChange>? OnContainerStateChanged { get; set; }

        public async Task<StartContainerResult> StartContainer(
            int agentId,
            string image,
            string name,
            string agentUserId,
            Dictionary<string, string> labels,
            Dictionary<string, string> environmentVariables,
            long memoryLimit,
            float cpuLimit,
            long reservedMemory,
            float reservedCpu,
            DateTime expiresOn,
            Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration crashReportConfiguration,
            CancellationToken cancellationToken)
        {
            ServerContainer serverContainer;
            lock (_lock)
            {
                if (reservedMemory + this.UsedMemory > this.TotalMemory)
                {
                    return new StartContainerResult { Success = false, Error = "unableToSatisfyResourceReservation" };
                }

                if (reservedCpu + this.UsedCpu > this.TotalCpu)
                {
                    return new StartContainerResult { Success = false, Error = "unableToSatisfyResourceReservation" };
                }
                serverContainer = new ServerContainer(agentId, name, image, DateTime.UtcNow, reservedMemory, reservedCpu, crashReportConfiguration, expiresOn);
                _trackedContainers.Add(name, serverContainer);
            }

            try
            {

                _logger.Log(LogLevel.Information, "Starting docker container {name} from image '{image}'.", name, image);
                var images = await _docker.Images.ListImagesAsync(new ImagesListParameters { All = true });
                if (!images.Any(i => i.RepoTags?.Contains(image) ?? false))
                {
                    _logger.Log(LogLevel.Information, "Downloading image {name}...", image);
                    await _docker.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = image }, new AuthConfig { }, NullDockerJsonMessageProgress.Instance);
                    images = await _docker.Images.ListImagesAsync(new ImagesListParameters { All = true });

                    if (!images.Any(i => i.RepoTags?.Contains(image) ?? false))
                    {
                        _logger.Log(LogLevel.Error, "Image {name} not found.", image);
                        throw new InvalidOperationException($"Image {image} not found.");
                    }
                    else
                    {
                        _logger.Log(LogLevel.Information, "Image {name} downloaded.", image);
                    }
                }


                var publicIp = _options.PublicIp;
                var portReservation = _portsManager.AcquirePort();
                environmentVariables["Stormancer_Server_Port"] = portReservation.Port.ToString();
                /*
                 { "Stormancer_Server_PublishedAddresses", server.PublicIp },
                    { "Stormancer_Server_PublishedPort", server.ServerPort.ToString() }
                 */
                environmentVariables["Stormancer_Server_PublishedAddresses"] = publicIp;
                environmentVariables["Stormancer_Server_PublishedPort"] = portReservation.Port.ToString();
                labels["stormancer.agent"] = AgentId;
                labels["stormancer.agent.userId"] = agentUserId;
                labels["stormancer.agent.clientId"] = agentId.ToString();
                labels["stormancer.reservedMemory"] = reservedMemory.ToString();
                labels["stormancer.reservedCpu"] = reservedCpu.ToString();
                labels["stormancer.crashReportConfig"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(crashReportConfiguration)));
                labels["stormancer.expiresOn"] = expiresOn.ToString();
                labels["stormancer.reservedPorts"] = portReservation.Port.ToString();

                CreateContainerParameters parameters = new CreateContainerParameters()
                {
                    Image = image,
                    Name = name,
                    Labels = labels,

                    HostConfig = new HostConfig()
                    {
                        NetworkMode = "host",
                        DNS = new[] {
                            "8.8.8.8",
                            "8.8.4.4"
                        },//Use Google DNS. 
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

                        Memory = memoryLimit,
                        CPUPeriod = 100000,
                        CPUQuota = (long)(100000 * cpuLimit),
                        Ulimits = new List<Ulimit> {
                            new Ulimit { Name = "core", Hard = -1, Soft = -1 }
                        }

                    },


                    Tty = true,
                    ExposedPorts = new Dictionary<string, EmptyStruct> { { portReservation.Port + "/udp", new EmptyStruct() } },
                    Env = environmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),


                };

                _logger.Log(LogLevel.Information, "Creating docker container from image {image}.", image);

                var response = await _docker.Containers.CreateContainerAsync(parameters);


                _logger.Log(LogLevel.Information, "Starting docker container {id} from image {image}.", response.ID, image);

                serverContainer.DockerContainerId = response.ID;
                serverContainer.AddResource(portReservation);

                var startResponse = await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters { });

                if (startResponse == false)
                {
                    throw new InvalidOperationException($"Docker container '{name}' failed to start.");
                }
                return new StartContainerResult { Success = true, Container = serverContainer };

            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _trackedContainers.Remove(name);
                }
                return new StartContainerResult { Success = false, Error = ex.ToString() };
            }

        }

        public async Task<bool> StopContainer(int agentId, string id, uint waitBeforeKillSeconds)
        {
            lock (_lock)
            {
                if (!_trackedContainers.Values.Any(c => c.DockerContainerId == id || c.Name == id))
                {
                    return false;
                }

            }
            _logger.Log(LogLevel.Information, "Stopping docker container {containerId}.", id);
            var result = await _docker.Containers.StopContainerAsync(id, new Docker.DotNet.Models.ContainerStopParameters { WaitBeforeKillSeconds = waitBeforeKillSeconds });

            lock (_lock)
            {
                _trackedContainers.Remove(id);
            }
            return result;
        }

        public async IAsyncEnumerable<ServerContainer> ListContainers(int agentId)
        {
            var response = await _docker.Containers.ListContainersAsync(new Docker.DotNet.Models.ContainersListParameters { All = true });


            foreach (var container in response)
            {
                var name = container.Names.FirstOrDefault()?.Trim('/');
                if (name != null && _trackedContainers.TryGetValue(name, out var server) && agentId == server.AgentId)
                {
                    yield return server;
                }

            }
        }

        private class ResponseSource<T> : IProgress<T>
        {
            private Subject<T> _completed = new Subject<T>();
            private readonly Func<IProgress<T>, CancellationToken, Task> _func;

            public ResponseSource(Func<IProgress<T>, CancellationToken, Task> func)
            {

                _func = func;
            }

            public void Report(T value)
            {
                _completed.OnNext(value);
            }

            public Task Run(CancellationToken cancellationToken)
            {
                return RunRequest(_func, cancellationToken);
            }
            private async Task RunRequest(Func<IProgress<T>, CancellationToken, Task> func, CancellationToken cancellationToken)
            {
                try
                {
                    await func(this, cancellationToken);
                    _completed.OnCompleted();
                }
                catch (Exception ex)
                {
                    _completed.OnError(ex);
                }
            }
            public IAsyncEnumerable<T> GetResponses(CancellationToken cancellationToken)
            {

                return GetObservable(cancellationToken).ToAsyncEnumerable();
            }

            public IObservable<T> GetObservable(CancellationToken cancellationToken)
            {
                return Observable.Create<T>(o =>
                {
                    _completed.Subscribe(o);
                    Run(cancellationToken);
                    return () => { };
                });
            }


        }
        public async IAsyncEnumerable<ContainerStatsResponse> GetContainerStatsAsync(int agentId, string name, bool stream, bool oneShot, CancellationToken cancellationToken)
        {
            var containerId = GetContainerIdByName(agentId, name);
            if (containerId == null)
            {
                yield break;
            }

            var source = new ResponseSource<ContainerStatsResponse>((progress, ct) => _docker.Containers.GetContainerStatsAsync(containerId, new ContainerStatsParameters { Stream = stream, OneShot = oneShot }, progress, ct));
            await foreach (var response in source.GetResponses(cancellationToken))
            {

                yield return response;
            }
        }

        private string? GetContainerIdByName(int agentId, string name)
        {
            lock (_lock)
            {
                if (_trackedContainers.TryGetValue(name, out var serverContainer) && serverContainer.AgentId == agentId)
                {
                    return serverContainer.DockerContainerId;
                }
                else
                {
                    return null;
                }
            }

        }

        private async Task<string?> GetContainerIdByNameFromDocker(int agentId, string name)
        {
            var containers = await _docker.Containers.ListContainersAsync(new ContainersListParameters { All = true, });
            foreach (var container in containers)
            {
                if (container.Names.Any(n => n.EndsWith(name)))
                {
                    return container.ID;
                }
            }
            return null;
        }

        internal async IAsyncEnumerable<IEnumerable<string>> GetContainerLogsAsync(int agentId, string name, DateTime? since, DateTime? until, uint size, bool follow, CancellationToken cancellationToken)
        {
            var containerId = await GetContainerIdByNameFromDocker(agentId, name);
            if (containerId == null)
            {
                yield break;
            }

            var source = new ResponseSource<string>((progress, ct) =>
            {
                var args = new ContainerLogsParameters { Follow = false, ShowStderr = true, ShowStdout = true, Timestamps = true };
                if (size > 0)
                {
                    args.Tail = size.ToString();
                }
                if (since != null)
                {
                    args.Since = since.ToString();

                }
                if (until != null)
                {
                    args.Until = until.ToString();
                }

                return _docker.Containers.GetContainerLogsAsync(containerId, args, cancellationToken, progress);
            });



            await foreach (var lines in source.GetObservable(cancellationToken).Buffer(TimeSpan.FromSeconds(1), 100).ToAsyncEnumerable())
            {
                yield return lines;
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
                catch (TimeoutException)
                {
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, "an error occured while querying docker for events : {ex}", ex);
                    await Task.Delay(1000);

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
            if (value.Status == "die" || value.Status == "stop")
            {
                ServerContainer? server;
                lock (_lock)
                {
                    server = _trackedContainers.FirstOrDefault(kvp => kvp.Value.DockerContainerId == value.ID).Value;
                    if (server != null)
                    {
                        _trackedContainers.Remove(server.Name);
                    }

                }
                if (server != null)
                {
                    using (server)
                    {

                        _logger.Log(LogLevel.Information, "Docker container {id} stopped.", value.ID);


                        _ = CreateCrashReportIfNecessary(server, CancellationToken.None);
                        this.OnContainerStateChanged?.Invoke(new ServerContainerStateChange { Container = server, Status = ContainerEventType.Stop });
                        _messager.PostServerStoppedMessage(server);

                    }
                }
            }
            else if (value.Status == "start")
            {
                if (_trackedContainers.TryGetValue(value.ID, out var server))
                {
                    this.OnContainerStateChanged?.Invoke(new ServerContainerStateChange { Container = server, Status = ContainerEventType.Start });
                }
            }
        }

        private async Task CreateCrashReportIfNecessary(ServerContainer server, CancellationToken cancellationToken)
        {
            if (server.DockerContainerId == null || !server.CrashReportConfiguration.Enabled)
            {
                return;
            }
            var statusCode = await GetStatusCode(server.DockerContainerId);

            if (statusCode != 0)
            {
                Directory.CreateDirectory("reports");
                using var archive = new ZipArchive(File.Create($"reports/{server.Name}.zip"));

                if (server.CrashReportConfiguration.IncludeOutput)
                {

                    var entry = archive.CreateEntry("logs.txt", CompressionLevel.SmallestSize);
                    using var writer = new StreamWriter(entry.Open());

                    await foreach (var block in GetContainerLogsAsync(server.AgentId, server.Name, null, null, 0, false, cancellationToken))
                    {
                        foreach (var log in block)
                        {
                            writer.WriteLine(log);
                        }
                    }
                }

                foreach (var file in server.CrashReportConfiguration.AdditionalContainerFiles)
                {
                    if (file.StartsWith('/'))
                    {
                        var containerFile = await GetContainerFile(server.DockerContainerId, file, cancellationToken);
                        if (containerFile != null)
                        {
                            var entry = archive.CreateEntry($"container{file}.tar", CompressionLevel.SmallestSize);

                            await containerFile.Stream.CopyToAsync(entry.Open(), cancellationToken);
                        }
                    }
                }

                if (server.CrashReportConfiguration.IncludeDump && _options.CorePath != null)
                {
                    var containerFile = await GetContainerFile(server.DockerContainerId, _options.CorePath, cancellationToken);
                    if (containerFile != null)
                    {
                        var entry = archive.CreateEntry($"core_dump.tar", CompressionLevel.SmallestSize);

                        await containerFile.Stream.CopyToAsync(entry.Open(), cancellationToken);
                    }
                }
            }
        }
        async Task<long> GetStatusCode(string containerId)
        {

            var inspectResult = await _docker.Containers.InspectContainerAsync(containerId);
            return inspectResult.State.ExitCode;

        }

        public Task<GetArchiveFromContainerResponse> GetContainerFile(string containerId, string path, CancellationToken cancellationToken)
        {
            return _docker.Containers.GetArchiveFromContainerAsync(containerId, new GetArchiveFromContainerParameters { Path = path }, false, cancellationToken);

        }
        internal async Task<AgentStatus> GetStatus()
        {
            try
            {
                var version = await _docker.System.GetVersionAsync();
                return new AgentStatus
                {
                    Claims = _options.Attributes,

                    AgentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                    DockerVersion = version.Version,
                    ReservedCpu = this.UsedCpu,
                    TotalCpu = TotalCpu,
                    ReservedMemory = UsedMemory,
                    TotalMemory = TotalMemory
                };
            }
            catch (Exception ex)
            {
                return new AgentStatus
                {
                    Claims = _options.Attributes,

                    AgentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty,
                    DockerVersion = string.Empty,
                    Error = ex.ToString()
                };
            }
        }

        internal bool KeepAlive(int agentId, string containerId, DateTime newExpirationTime)
        {
            if (_trackedContainers.TryGetValue(containerId, out var container))
            {
                container.ExpiresOn = newExpirationTime;
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class AgentStatus
    {
        public Dictionary<string, string> Claims { get; set; }

        public string DockerVersion { get; set; }

        public string AgentVersion { get; set; }
        public string Error { get; set; }

        public float ReservedCpu { get; set; }


        public float TotalCpu { get; set; }


        public long ReservedMemory { get; set; }


        public long TotalMemory { get; set; }
    }

    public class ServerContainer : IDisposable
    {
        internal ServerContainer(int agentId, string name, string image, DateTime created, long memory, float cpuCount, Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration crashReportConfiguration, DateTime expiresOn)
        {
            AgentId = agentId;
            Name = name;
            Image = image;
            Created = created;
            Memory = memory;
            CpuQuota = cpuCount;
            CrashReportConfiguration = crashReportConfiguration;
            ExpiresOn = expiresOn;
        }

        public int AgentId { get; }
        public string? DockerContainerId { get; set; }
        public string Name { get; }
        public string Image { get; }
        public DateTime Created { get; }
        public long Memory { get; }
        public float CpuQuota { get; }
        public Server.Plugins.GameSession.ServerProviders.CrashReportConfiguration CrashReportConfiguration { get; }

        /// <summary>
        /// Gets or sets the time the container expires on.
        /// </summary>
        public DateTime ExpiresOn { get; set; }

        public void AddResource(IDisposable resource)
        {
            _resources.Add(resource);
        }

        private List<IDisposable> _resources = new List<IDisposable>();

        /// <inheritdoc/>
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
        public ContainerEventType Status { get; internal set; }
    }

    public class GetLogsResult
    {
        public string StdOut { get; set; }
        public string StdErr { get; set; }
    }
}
