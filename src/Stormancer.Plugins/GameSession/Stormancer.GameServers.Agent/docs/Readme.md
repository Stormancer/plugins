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