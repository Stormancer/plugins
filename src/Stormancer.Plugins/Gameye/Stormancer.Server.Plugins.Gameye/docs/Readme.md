# Gameye plugin


Enables the Stormancer game session system to host game servers in Gameye.

## Installation
### Client


    //Include the plugin code
    #include "gameye.hpp"
    

    [...]
    
    // In the Stormancer library configuration code, add and configure the Gameye plugin.

    conf->additionalParameters[Stormancer::Gameye::ConfigurationKeys::GameyePortId] = "7777";
	conf->addPlugin(new Stormancer::Gameye::GameyePlugin());


### Server

* Add the `Stormancer.Server.Plugins.Gameye` NuGet package to the application server project. 
* Declare a Gameye game server pool and configure the game session system to use it.
