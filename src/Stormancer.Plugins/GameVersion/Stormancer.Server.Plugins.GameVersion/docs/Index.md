Game version plugin
===================

This plugin validates clients before authentication using a provided version.


Server configuration
--------------------

The target client versions  is set in the application config :

    "clientVersion":{
        "authorizedVersions":["1.45.*"],          //The version string the client must adhere to for authentication to be accepted.
        "enableVersionChecking":true //enables version checking (defaults to false)
    }

If the version string ends with `*`, the version check switches from exact match to prefix match. For instance in the above example, any version string starting with `1.45.` are accepted. 


Client configuration
--------------------

- Add the GameVersion plugin to the client.
- Set the game version in additional client configuration.