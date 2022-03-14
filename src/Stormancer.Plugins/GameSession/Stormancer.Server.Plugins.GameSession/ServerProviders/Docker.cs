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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stormancer.Server.Plugins.GameSession
{

    class DockerGameServerProvider : IGameServerProvider, IDisposable, IProgress<Message>
    {
        private readonly ILogger _logger;
        private readonly IEnvironment _environment;
        private readonly IDataProtector dataProtector;
        private readonly IDelegatedTransports pools;

        private readonly DockerClient _docker;



        private class GameServerContainer : IDisposable
        {
           
            public GameServerContainer(GameServerInstance instance,params ILease[] leases)
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
            while(_docker !=null)
            {
                await Task.Delay(500);

                try
                {
                    await _docker.System.MonitorEventsAsync(new ContainerEventsParameters { Since = _monitorSince.ToString() }, this);
                }
                catch(Exception ex)
                {
                    _logger.Log(LogLevel.Error, "docker", "an error occured while calling Instance.OnClosed.", ex);
                }
                


            }
        }
        public void Report(Message value)
        {
            _monitorSince = DateTime.UtcNow;
            if (value.Status == "stop")
            {
                var server = _servers.Values.FirstOrDefault(s => s.ContainerId == value.ID);
                if(server!=null)
                {
                    try
                    {
                        server.Instance.OnClosed?.Invoke();
                    }
                    catch(Exception ex)
                    {
                        _logger.Log(LogLevel.Error, "docker", "an error occured while calling Instance.OnClosed.", ex);
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
            var config = c.ToObject<DockerPoolConfiguration>();
            if (config == null || config.image == null)
            {
                throw new InvalidOperationException("'image' must be set in a docker GameServer provider.");
            }

            var applicationInfo = await _environment.GetApplicationInfos();
            var fed = await _environment.GetFederation();


            var arguments = string.Join(" ", config.arguments);

            var server = await LeaseServerPort(c);

           

            //Token used to authenticate the DS with the DedicatedServerAuthProvider

            var authenticationToken = await dataProtector.ProtectBase64Url(Encoding.UTF8.GetBytes(id), "gameServer");

            var endpoints = string.Join(',', fed.current.endpoints);
            var environmentVariables = new Dictionary<string, string>();
            //startInfo.EnvironmentVariables.Add("connectionToken", token);
            environmentVariables.Add("Stormancer.Server.Port", server.ServerPort.ToString());
            environmentVariables.Add("Stormancer.Srver.ClusterEndpoints", endpoints);
            environmentVariables.Add("Stormancer.Server.PublishedAddresses", server.PublicIp);
            environmentVariables.Add("Stormancer.Server.PublishedPort", server.ServerPort.ToString());
            environmentVariables.Add("Stormancer.Server.AuthenticationToken", authenticationToken);
            environmentVariables.Add("Stormancer.Server.Account", applicationInfo.AccountId);
            environmentVariables.Add("Stormancer.Server.Application", applicationInfo.ApplicationName);
            environmentVariables.Add("Stormancer.Server.Arguments", arguments);
           

            var response = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters()
            {
                Image = config.image,
                Name = id,
                HostConfig = new HostConfig()
                {
                    DNS = new[] { "8.8.8.8", "8.8.4.4" }
                },
                ExposedPorts = new Dictionary<string, EmptyStruct> { { server.ServerPort.ToString(), new EmptyStruct() } },
                 
            });

            server.ContainerId = response.ID;
          
            var startResponse = await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters {  });


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

        public async Task StopServer(string id)
        {
            if (_servers.TryRemove(id, out var server))
            {
                if (await _docker.Containers.StopContainerAsync(server.ContainerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }))
                {

                }

            }
        }

        private async Task<GameServerContainer> LeaseServerPort(dynamic config)
        {
            var serverLease = await pools.AcquirePort((string)config.serverPool ?? "public1");
            if (!serverLease.Success)
            {

                throw new InvalidOperationException("Unable to acquire port for the server");
            }
            var serverGuid = Guid.NewGuid();
            var result = new GameServerInstance { Id = serverGuid };
            var server = new GameServerContainer(result,serverLease)
            {
                ServerPort = serverLease.Port,
                PublicIp = serverLease.PublicIp

            };

            return server;
        }

        public void Dispose()
        {
            if(_docker != null)
            {
                var docker = _docker;
            
                docker.Dispose();
            }
        }
    }
}
