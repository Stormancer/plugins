`Stormancer.Server.Plugins.PeerConfiguration` provides a way to push json configuration from the server to clients on startup and in almost real time whenever the configuration is changed on the server.

## Setting up the client configuration on the server

The client configuration is set as a section of the server app configuration. It is automatically pushed to clients whenever the server configuration is updated and there is changes in the client configuration section.

    {
        "peerConfig":
        {
            "foo":"bar"
        }
    }

## Receiving the configuration on the client

    #include "configuration/PeerConfiguration.hpp"