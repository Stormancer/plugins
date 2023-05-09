# Overview

The Users plugin provides basic authentication and identity features for peers connecting to a Stormancer server application.
The following features are supported:

- User ids scoped to the application, used by other plugins and clients.
- Extensible identity system. Plugins can implement `IAuthenticationProvider` to add new authentication capabilities to the application. The Steam, PSN, XBoxLive, Epic, Gog and Nintendo plugins integrate with the Users plugin out of the box. Dev, DeviceId and Login/password are also provided.
- Federated identity : an user of the application can authenticate with multiple providers
- Integration with the profile system to expose user related informations as a profile part.
- User search.
- In memory session data.
- Service locator functionalities (should be moved to another plugin)
- Integration with the analytics plugin.


# Requesting a scene token

