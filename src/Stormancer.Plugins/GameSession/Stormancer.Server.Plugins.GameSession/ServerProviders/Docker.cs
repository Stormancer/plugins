// MIT License
//
// Copyright (c) 2019 Stormancer
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stormancer.Diagnostics;
using Stormancer.Server.Components;
using Stormancer.Server.Plugins.DataProtection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{
    class NullDockerJsonMessageProgress : IProgress<JSONMessage>
    {
        public void Report(JSONMessage value)
        {

        }

        public static NullDockerJsonMessageProgress Instance { get; } = new NullDockerJsonMessageProgress();
    }
    class DockerGameServerProvider : IGameServerProvider, IDisposable, IProgress<Message>
    {
        private readonly ILogger _logger;
        private readonly IEnvironment _environment;
        private readonly IDataProtector dataProtector;
        private readonly IDelegatedTransports pools;

        private readonly DockerClient _docker;
        private bool _shouldMonitorDocker = false;


        private class GameServerContainer : IDisposable
        {

            public GameServerContainer(GameServerInstance instance, params ILease[] leases)
            {
                Instance = instance;
                PortLeases = leases;

            }

            public GameServerInstance Instance { get; }
            public ILease[] PortLeases { get; set; }

            public ushort ServerPort { get; set; }

            public string PublicIp { get; set; } = "";

            public string ContainerId { get; set; }
            public void Dispose()
            {
                foreach (var lease in PortLeases)
                {

                    lease.Dispose();
                }
            }


        }

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
                    _logger.Log(LogLevel.Warn, "docker", "an error occured while querying docker for events.", ex);
                    await Task.Delay(10000);
                }
            }
        }
        public void Report(Message value)
        {
            _monitorSince = DateTime.UtcNow;
            if (value.Status == "stop")
            {
                var server = _servers.Values.FirstOrDefault(s => s.ContainerId == value.ID);
                if (server != null)
                {
                    using (server)
                    {
                        try
                        {
                            _logger.Log(LogLevel.Info, "docker", $"Docker container {value.ID} stopped.", new { container = server.ContainerId });

                            server.Instance.OnClosed?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(LogLevel.Error, "docker", "an error occured while calling Instance.OnClosed.", ex);
                        }
                    }
                }
            }
        }


        private ConcurrentDictionary<string, GameServerContainer> _servers = new ConcurrentDictionary<string, GameServerContainer>();
        private DateTime _monitorSince;

        public string Type => "docker";

        public DockerGameServerProvider(
            IEnvironment env,
            IDataProtector dataProtector,
            IDelegatedTransports pools,
            ILogger logger)
        {
            _logger = logger;
            _environment = env;
            this.dataProtector = dataProtector;
            this.pools = pools;
            var dockerConfig = new DockerClientConfiguration();


            _docker = dockerConfig.CreateClient();
            _monitorSince = DateTime.UtcNow;

            _ = StartMonitorDocker();

        }

        public async Task<GameServerInstance> StartServer(string id, JObject c, CancellationToken ct)
        {
            _shouldMonitorDocker = true;
            var config = c.ToObject<DockerPoolConfiguration>();
            if (config == null || config.image == null)
            {
                throw new InvalidOperationException("'image' must be set in a docker GameServer provider.");
            }

            var applicationInfo = await _environment.GetApplicationInfos();
            var fed = await _environment.GetFederation();
            var node = await _environment.GetNodeInfos();

            var arguments = string.Join(" ", config.arguments ?? Enumerable.Empty<string>());

            var udpTransports = node.Transports.First(t => t.Item1 == "raknet");

            var server = await LeaseServerPort(c);
            try
            {

                //Token used to authenticate the DS with the DedicatedServerAuthProvider

                var authenticationToken = await dataProtector.ProtectBase64Url(Encoding.UTF8.GetBytes(id), "gameServer");

                var endpoints = string.Join(',', fed.current.endpoints.Select(e => TransformEndpoint(e)));


                _logger.Log(LogLevel.Info, "docker", "creating docker container.", new { image = config.image });
                var images = await _docker.Images.ListImagesAsync(new ImagesListParameters { All = true });
                if (!images.Any(i => i.RepoTags.Contains(config.image)))
                {
                    await _docker.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = config.image }, new AuthConfig { }, NullDockerJsonMessageProgress.Instance);
                }

                var environmentVariables = new Dictionary<string, string>
                {
                    //startInfo.EnvironmentVariables.Add("connectionToken", token);
                    { "Stormancer_Server_Port", server.ServerPort.ToString() },
                    { "Stormancer_Server_ClusterEndpoints", endpoints },
                    { "Stormancer_Server_PublishedAddresses", server.PublicIp },
                    { "Stormancer_Server_PublishedPort", server.ServerPort.ToString() },
                    { "Stormancer_Server_AuthenticationToken", authenticationToken },
                    { "Stormancer_Server_Account", applicationInfo.AccountId },
                    { "Stormancer_Server_Application", applicationInfo.ApplicationName },
                    { "Stormancer_Server_Arguments", arguments },
                    { "Stormancer_Server_TransportEndpoint", TransformEndpoint(udpTransports.Item2.First().Replace(":","|")) }
                };

                CreateContainerParameters parameters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new CreateContainerParameters()
                {
                    Image = config.image,
                    Name = id.Substring(id.IndexOf('/') + 1),
                    Labels = new Dictionary<string, string> { ["host"] = applicationInfo.HostUrl },
                    HostConfig = new HostConfig()
                    {

                        DNS = new[] { "8.8.8.8", "8.8.4.4" },
                        PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            [server.ServerPort + "/udp"] = new List<PortBinding>
                             {
                                 new PortBinding
                                 {
                                     HostIP = server.PublicIp,
                                     HostPort = server.ServerPort.ToString()
                                 }
                             }
                        }

                    },

                    ExposedPorts = new Dictionary<string, EmptyStruct> { { server.ServerPort + "/udp", new EmptyStruct() } },
                    Env = environmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),

                } :
                new CreateContainerParameters()
                {
                    Image = config.image,
                    NetworkDisabled = false,

                    Labels = new Dictionary<string, string> { ["host"] = applicationInfo.HostUrl },
                    Name = id.Substring(id.IndexOf('/') + 1),
                    HostConfig = new HostConfig()
                    {
                        NetworkMode = "host",
                        DNS = new[] { "8.8.8.8", "8.8.4.4" },

                    },
                    ExposedPorts = new Dictionary<string, EmptyStruct> { { server.ServerPort + "/udp", new EmptyStruct() } },
                    Env = environmentVariables.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),

                };

                var response = await _docker.Containers.CreateContainerAsync(parameters);


                server.ContainerId = response.ID;
                _logger.Log(LogLevel.Info, "docker", "starting docker container.", new { image = config.image, container = response.ID });
                var startResponse = await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters { });


                if (startResponse)
                {
                    _servers.TryAdd(id, server);
                    return server.Instance;
                }
                else
                {
                    throw new InvalidOperationException($"Failed to start container {response.ID}");
                }
            }
            catch
            {
                server.Dispose();
                throw;
            }



        }

        private string TransformEndpoint(string endpoint)
        {
            var host = GetHost();
            return endpoint.Replace("localhost", host).Replace("127.0.0.1", host);

        }
        private string GetHost() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "172.17.0.1" : "172.17.0.1";



        public async Task StopServer(string id)
        {

            if (_servers.TryRemove(id, out var server))
            {

                _logger.Log(LogLevel.Info, "docker", "stopping docker container.", new { container = server.ContainerId });

                if (await _docker.Containers.StopContainerAsync(server.ContainerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }))
                {
                    await _docker.Containers.RemoveContainerAsync(server.ContainerId, new ContainerRemoveParameters { Force = true });
                }


            }
        }

        private async Task<GameServerContainer> LeaseServerPort(dynamic config)
        {
            var serverLease = await pools.AcquirePort((string)config.portPoolId ?? "public");
            if (!serverLease.Success)
            {

                throw new InvalidOperationException("Unable to acquire port for the server");
            }
            var serverGuid = Guid.NewGuid();
            var result = new GameServerInstance { Id = serverGuid };
            var server = new GameServerContainer(result, serverLease)
            {
                ServerPort = serverLease.Port,
                PublicIp = serverLease.PublicIp

            };

            return server;
        }

        public void Dispose()
        {
            if (_docker != null)
            {
                var docker = _docker;

                docker.Dispose();
            }
        }
    }
}
