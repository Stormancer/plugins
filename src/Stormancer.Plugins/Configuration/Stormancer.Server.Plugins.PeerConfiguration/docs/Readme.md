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

### Adding the plugin

    #include "configuration/PeerConfiguration.hpp"

    ...
    //Add the plugin to the stormancer client.
    conf->addPlugin(new Stormancer::PeerConfiguration::PeerConfigurationPlugin());

### API

    //Gets a boolean indicating whether the configuration is currently available.
    //The configuration is first sent just after authentication, so isAvailable returns false before that,
    //or if the plugin is not enabled on the server.
    bool isAvailable()

    //Get the current configuration as a json string. Returns an empty string if the configuration is not available.
    std::string get()

    //Subscribes to changes to the configuration.
    Stormancer::Subscription subscribe(std::function<void(std::string)> callback, bool /*includeAlreadyReceived*/ = true)

