The Stormancer.GameServer.Agent tool provides a way to control a docker daemon to start, monitor and stop gameservers linked to Stormancer gamesessions.

It's packaged as a dotnet tool to simplify installation and updates.

# Installation

## Prerequisites
- you need the to have the dotnet 11 SDK installed (https://learn.microsoft.com/en-us/dotnet/core/install/)
- you need to have a docker service active
> apt-get install docker.io

## Install the agent
First create a tool manifest in the folder you want your agent to run from
```
> dotnet new tool-manifest
```

Then install the Stormancer.GameServer.Agent tool
```
> dotnet tool install --local Stormancer.GameServers.Agent --version <current-version>
> dotnet tool restore
```

Updating the agent:
```
> dotnet tool update Stormancer.GameServers.Agent --version {targetVersion} 
> dotnet tool restore
```

See [dotnet tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) for more information.` 
# Configuration

    "Agent": {
      "StormancerEndpoint": "https://serverendpoint.com",
      "StormancerAccount": "account",
      "StormancerApplication": "app",
      "PrivateKeyPassword": "password",
      "PrivateKeyPath": "<path-to-the-private-key-authenticating-the-agent.pfx>",
      "MinPort": 40000,
      "MaxPort": 40999
    }


## Ports & Firewall

When starting a container, the agent associates it with a port in the range provided in the config (defaults to `[40000,40999]`). The firewall of the server MUST be opened for UDP trafic, otherwise the Stormancer client library will need to switch to NAT traversal or relay to establish connectivity between the game server in the container and game clients.

Remark: The agent is aggressive when reusing ports: Whenever a container is started, it tries to associates it with the smallest port available. Consequently the agent won't ever allocate a port higher than the total number of containers running concurrently.

# Setting up core dumps

1. Run `echo '/tmp/core-dump.%p' | sudo tee /proc/sys/kernel/core_pattern` on the agent to setup the host core pattern.

For more information about the core_pattern : https://www.kernel.org/doc/html/latest/admin-guide/sysctl/kernel.html#core-pattern
    
To make the change in the host core pattern permanent, add `kernel.core_pattern = /tmp/core-dump.%p` to /etc/sysctl.conf, then reload it with `sysctl -p`

2. Set the `CorePath` value in the agent configuration to the path of the generated core dump in the container.

    "Agent":{
       "CorePath":"/tmp/core-dump.PID"
    }

Once it's done, if a game server doesn't stop with exit code 0, the agent will zip the server logs, the set of files specified in the app config and the generated core dump, and store the resulting archive in the diagnostics system for later download.

# Agent as a service (ubuntu)

Create a service file (gameservers-agent.service) in /etc/systemd/system using nano or your favorite text editor
> sudo nano /etc/systemd/system/gameservers-agent.service

 - A sample service file would be as follow:

```
[Unit]
Description=Game server agent
After=network-online.target

[Service]
Type=simple

#a user with sufficient permissions
User=root 

ExecStart=/usr/bin/dotnet tool run stormancer-gameservers-agent
#use your agent installation folder as WorkingDirectory
WorkingDirectory=/home/agent

Restart=on-failure

TimeoutStopSec=30

[Install]
WantedBy=multi-user.target
```

Remark: for more robustness, we recommend using a batch script that restore then run the tool as the ExecStart command of the unit file.

- Enable the service
```
> sudo systemctl daemon-reload
> sudo systemctl enable gameservers-agent.service
```
- Start the service
```
> sudo systemctl start gameservers-agent.service
```