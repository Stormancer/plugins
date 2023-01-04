
This plugin adds support for P2P & client-server game sessions to a Stormancer application. It supports:

- P2P and Client/Server game sessions
- Electing an host in P2P game sessions
- Managing game servers (requires adding ServerPools.hpp to the game server)
- Game session startup/shutdown events
- Game session results aggregation and processing in P2P (allows comparing game results from all peers) and Client/server (only the server sends game results)
- Public or access restricted game sessions.


## Setting up game servers
The gamesession system supports starting game servers to run the game session.

### Development game servers
The development game server pool enables gamesessions to wait for the connection of a non authenticated gameserver
to host the game session. This enables starting the game server manually from a developer computer, in the debugger 
or on the cmd prompt.The clients and game server uses the normal P2P connectivity system, supporting NAT traversal.
This avoids the need of any configuration on the developer network. 

    ctx.HostStarting += (IHost host) =>
    {
        [...]

        //Declares a development server pool named dev.
        host.ConfigureServerPools(c => c.DevPool("dev"));

        //Declares a gamesession type using the pool configured above.
        host.ConfigureGameSession("gamesession-server", c => c
            .UseGameServer(c => c
                .PoolId("dev")
                )
            .CustomizeScene(scene=>scene.AddSocket())
        );

        [...]
    }

### Hosting game servers on local docker
The docker server pool enables game servers to be run as docker container on Stormancer cluster. Containers are
automatically started on the node running the less game instances. Other policies could be put in place if
necessary.

    ctx.HostStarting += (IHost host) =>
    {
        [...]

        //Declares a docker server pool named docker, that uses the image 'game-server-image:latest'
        host.ConfigureServerPools(c => c.DockerPool("docker", b => b.Image("game-server-image:latest")));

        //Declares a gamesession type using the pool configured above.
        host.ConfigureGameSession("gamesession-server", c => c
            .UseGameServer(c => c
                .PoolId("docker")
                )
            .CustomizeScene(scene=>scene.AddSocket())
        );

        [...]
    }

Other tasks are required to host game servers on docker:

#### Grid configuration
For the game servers to connect to the Stormancer cluster from inside docker containers, the grid nodes must **not**
be bound to localhost. They **must** publish public, or at least LAN IP addresses. To do that, in the node
configuration file, set both `publicIp` and `loadbalancedIp` to an interface accessible by docker containers, 
for instance:

    {
        "constants": {
            "host-ip" : "xxx.xxx.xxx.xxx"
            "publicIp": "{host-ip}",
            "loadBalancedIp": "{host-ip}",
            [...]

Stormancer needs to associate local ports to the game server. To specify the range of ports to use, a `delegated`
transport entry must be added to the grid nodes configuration:


    [...]

    "endpoints": {
        //udp transport
        "udp1": {
            "type": "raknet",
            "port": "{udpPort}",
            "maxConnections": 100,
            "publicEndpoint": "{publicIp}:{udpPort}"
        },
        
        [...]
        
        //delegated transport, allows specifying a pool of ports.
        "public1": {
            "type": "delegated",
            "publicEndpoint": "{publicIp}",
            "ports": {
            "min": 42000,
            "max": 44000

            }

        }
    },
    [...]

The node firewall must be opened for UDP in the range specified (42000-44000 in the example).


### Secrets
Docker servers authentify with the grid using an encrypted token using the aes-gcm algorithm. The encryption key
is stored in a cluster secret store that must be created manually. The path to the key is specified in the 
application' configuration in the `gameServer` dataProtection policy:

    {
        [...]

	    "dataProtection":{
		    "gameServer":{
			    "provider":"aes-gcm",
			    "key":"my-account/my-secret-store/gameServer",
			    "createKeyIfNotExists":true
		    }
            [...]
	    }
    }
 
The secrets store must be created prior to application start. If the CLI plugin `Stormancer.Management.CLI` is
installed, it can be created using the following command:

    > dotnet tool run stormancer manage secrets-store create --cluster test --account my-account --id my-secret-store

