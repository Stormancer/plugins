The Stormancer.GameServer.Agent tool provides a way to control a docker daemon to start, monitor and stop gameservers linked to Stormancer gamesessions.



Configuration
-------------

  "Agent": {
    "StormancerEndpoint": "https://serverendpoint.com",
    "StormancerAccount": "account",
    "StormancerApplication": "app",
    "PrivateKeyPassword": "password",
    "PrivateKeyPath": "<path-to-the-private-key-authenticating-the-agent.pfx>"
  }


Setting up core dumps
---------------------

1. Run `echo '/tmp/core-dump.%p' | sudo tee /proc/sys/kernel/core_pattern` on the agent to setup the host core pattern.

For more information about the core_pattern : https://www.kernel.org/doc/html/latest/admin-guide/sysctl/kernel.html#core-pattern

2. Set the `CorePath` value in the agent configuration to the path of the generated core dump in the container.

    "Agent":{
       "CorePath":"/tmp/core-dump.PID"
    }

Once it's done, if a game server doesn't stop with exit code 0, the agent will zip the server logs, the set of files specified in the app config and the generated core dump, and store the resulting archive in the diagnostics system for later download.